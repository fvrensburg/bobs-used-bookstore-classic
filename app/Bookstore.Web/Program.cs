using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
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
using Bookstore.Web.Helpers;
using Bookstore.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using System;
using System.Collections.Generic;
using System.Security.Claims;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ------------------------------------------------------------------
    // Load additional settings from AWS SSM Parameter Store if configured.
    // Values are injected into IConfiguration so all downstream code reads
    // from the standard IConfiguration abstraction — no static singletons.
    // ------------------------------------------------------------------
    LoadAwsParameterStoreOverrides(builder.Configuration);

    // ------------------------------------------------------------------
    // Logging – NLog
    // ------------------------------------------------------------------
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // ------------------------------------------------------------------
    // MVC + Razor Views
    // ------------------------------------------------------------------
    builder.Services.AddControllersWithViews(options =>
    {
        // Global authorization filter – all controllers require auth by default
        options.Filters.Add(typeof(AuthorizeAttribute));
    });

    // ------------------------------------------------------------------
    // Database — connection string is resolved at request time so SSM
    // overrides loaded above are honoured.
    // ------------------------------------------------------------------
    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        options.UseSqlServer(config.GetConnectionString("BookstoreDatabaseConnection"));
    });

    // ------------------------------------------------------------------
    // Application services
    // ------------------------------------------------------------------
    builder.Services.AddScoped<IBookService, BookService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddScoped<IOfferService, OfferService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
    builder.Services.AddScoped<IImageResizeService, ImageResizeService>();
    builder.Services.AddScoped<ICheckoutService, CheckoutService>();

    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IAddressRepository, AddressRepository>();
    builder.Services.AddScoped<IBookRepository, BookRepository>();
    builder.Services.AddScoped<IOfferRepository, OfferRepository>();
    builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

    builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

    // ------------------------------------------------------------------
    // File service
    // ------------------------------------------------------------------
    if (builder.Configuration["Services/FileService"] == "aws")
    {
        builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client());
        builder.Services.AddScoped<IFileService, S3FileService>();
    }
    else
    {
        builder.Services.AddScoped<IFileService>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            return new LocalFileService(env.WebRootPath);
        });
    }

    // ------------------------------------------------------------------
    // Image validation service
    // ------------------------------------------------------------------
    if (builder.Configuration["Services/ImageValidationService"] == "aws")
    {
        builder.Services.AddSingleton<IAmazonRekognition>(new AmazonRekognitionClient());
        builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
    }
    else
    {
        builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
    }

    // ------------------------------------------------------------------
    // Authentication
    // ------------------------------------------------------------------
    if (builder.Configuration["Services/Authentication"] == "aws")
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.ClientId = builder.Configuration["Authentication/Cognito/LocalClientId"];
            options.MetadataAddress = builder.Configuration["Authentication/Cognito/MetadataAddress"];
            options.ResponseType = "code";
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.SaveTokens = true;
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
                    var request = context.Request;
                    var returnUrl = $"{request.Scheme}://{request.Host}/signin-oidc";
                    context.ProtocolMessage.RedirectUri = returnUrl;
                    return System.Threading.Tasks.Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;

                    var dto = new CreateOrUpdateCustomerDto(
                        identity.GetSub(),
                        identity.Name ?? string.Empty,
                        identity.FindFirst(c => c.Type.Contains("givenname"))?.Value ?? string.Empty,
                        identity.FindFirst(c => c.Type.Contains("surname"))?.Value ?? string.Empty);

                    await service.CreateOrUpdateCustomerAsync(dto);
                }
            };
        });
    }
    else
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Authentication/Login";
            });

        builder.Services.AddScoped<LocalAuthenticationMiddleware>();
    }

    builder.Services.AddHttpContextAccessor();

    // ------------------------------------------------------------------
    // Build the app
    // ------------------------------------------------------------------
    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // Local auth middleware only when not using Cognito
    if (builder.Configuration["Services/Authentication"] != "aws")
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    // ------------------------------------------------------------------
    // Routes
    // ------------------------------------------------------------------
    app.MapControllerRoute(
        name: "AdminArea",
        pattern: "Admin/{controller}/{action}/{id?}",
        defaults: new { area = "Admin" },
        constraints: null);

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped due to an exception.");
    throw;
}
finally
{
    LogManager.Shutdown();
}

// ---------------------------------------------------------------------------
// Load AWS SSM Parameter Store values into IConfiguration so that all
// application code reads through the standard IConfiguration abstraction.
// ---------------------------------------------------------------------------
static void LoadAwsParameterStoreOverrides(IConfigurationManager configuration)
{
    var rootPath = "/" + Constants.AppName;
    var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if (configuration["Services/Database"] == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParameterRequest
            {
                Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
            };
            var response = client.GetParameterAsync(request).GetAwaiter().GetResult();

            // Connection strings live under ConnectionStrings: in IConfiguration
            overrides["ConnectionStrings:BookstoreDatabaseConnection"] = response.Parameter.Value;
        }
        catch (Exception ex)
        {
            LogManager.GetCurrentClassLogger().Warn(ex, "Could not load database connection string from SSM.");
        }
    }

    if (configuration["Services/Authentication"] == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest
            {
                Path = $"{rootPath}/Authentication/",
                Recursive = true
            };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
            {
                var key = parameter.Name.Replace($"{rootPath}/", string.Empty);
                overrides[key] = parameter.Value;
            }
        }
        catch (Exception ex)
        {
            LogManager.GetCurrentClassLogger().Warn(ex, "Could not load authentication settings from SSM.");
        }
    }

    if (configuration["Services/FileService"] == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest
            {
                Path = $"{rootPath}/Files/",
                Recursive = true
            };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
            {
                var key = parameter.Name.Replace($"{rootPath}/", string.Empty);
                overrides[key] = parameter.Value;
            }
        }
        catch (Exception ex)
        {
            LogManager.GetCurrentClassLogger().Warn(ex, "Could not load file service settings from SSM.");
        }
    }

    // Inject SSM overrides into IConfiguration — these take precedence over
    // appsettings.json values since AddInMemoryCollection is the last source.
    if (overrides.Count > 0)
    {
        configuration.AddInMemoryCollection(overrides);
    }
}
