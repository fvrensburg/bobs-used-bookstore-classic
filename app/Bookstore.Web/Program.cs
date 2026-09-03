using Amazon.Rekognition;
using Amazon.S3;
using BobsBookstoreClassic.Data;
using Bookstore.Common;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Pull AWS SSM parameters into IConfiguration when running in AWS.
    // The provider maps /{AppName}/Key/Sub → Key:Sub so that BookstoreConfiguration
    // can continue using the legacy "Key/Sub" slash-separated accessor pattern.
    // Running locally (all services == "local") this block is skipped entirely.
    bool runningInAws = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV") != null
        || builder.Configuration["Services:Database"] == "aws"
        || builder.Configuration["Services:Authentication"] == "aws"
        || builder.Configuration["Services:FileService"] == "aws";

    if (runningInAws)
    {
        builder.Configuration.AddSystemsManager(
            $"/{Constants.AppName}",
            optional: true,
            reloadAfter: TimeSpan.FromMinutes(5));
    }

    // Initialize the static BookstoreConfiguration façade from the final IConfiguration
    // (includes SSM values when running in AWS, appsettings.json values locally).
    BookstoreConfiguration.Initialize(builder.Configuration);

    // Configure NLog
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();
    LoggingSetup.ConfigureLogging();

    // Configure MVC with global authorization filter (requires authenticated user app-wide;
    // individual controllers/actions can opt out with [AllowAnonymous])
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
    });

    // Configure EF Core
    var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection")
        ?? builder.Configuration.GetConnectionString("BookstoreDatabaseConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Register domain services
    builder.Services.AddScoped<IBookService, BookService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddScoped<IOfferService, OfferService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
    builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

    // Register repositories
    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IAddressRepository, AddressRepository>();
    builder.Services.AddScoped<IBookRepository, BookRepository>();
    builder.Services.AddScoped<IOfferRepository, OfferRepository>();
    builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

    // Register file service
    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        builder.Services.AddScoped<IAmazonS3, AmazonS3Client>();
        builder.Services.AddScoped<IFileService, S3FileService>();
    }
    else
    {
        // Save uploaded images alongside the existing seed cover images
        var imagesPath = Path.Combine(builder.Environment.ContentRootPath, "Content");
        builder.Services.AddSingleton<IFileService>(new LocalFileService(imagesPath));
    }

    // Register image validation service
    if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
    {
        builder.Services.AddScoped<IAmazonRekognition, AmazonRekognitionClient>();
        builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
    }
    else
    {
        builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
    }

    // Configure authentication
    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.ClientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
            options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.UsePkce = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.UseTokenLifetime = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "cognito:username",
                RoleClaimType = "cognito:groups"
            };
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.RedirectUri =
                        $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                    return Task.CompletedTask;
                },
                OnAuthorizationCodeReceived = context =>
                {
                    if (context.TokenEndpointRequest != null)
                        context.TokenEndpointRequest.RedirectUri =
                            $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                    var identity = context.Principal?.Identity as ClaimsIdentity;

                    if (identity == null) return;

                    var dto = new CreateOrUpdateCustomerDto(
                        identity.GetSub(),
                        identity.Name ?? string.Empty,
                        identity.FindFirst(y => y.Type.Contains("givenname"))?.Value ?? string.Empty,
                        identity.FindFirst(y => y.Type.Contains("surname"))?.Value ?? string.Empty);

                    await service.CreateOrUpdateCustomerAsync(dto);
                }
            };
        });
    }
    else
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();
        builder.Services.AddScoped<LocalAuthenticationMiddleware>();
    }

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Seed the database
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await BookstoreDbSeeder.SeedAsync(dbContext);
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Admin/Error/Support");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Serve legacy Content/ and Scripts/ folders from their original locations.
    // Views reference paths like /Content/css/site.css and /Scripts/jquery/jquery.min.js.
    foreach (var (dir, req) in new[] { ("Content", "/Content"), ("Scripts", "/Scripts") })
    {
        var fullPath = Path.Combine(builder.Environment.ContentRootPath, dir);
        if (Directory.Exists(fullPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(fullPath),
                RequestPath = req
            });
        }
    }

    app.UseRouting();

    // Local auth middleware must run before UseAuthentication/UseAuthorization
    if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped due to exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
