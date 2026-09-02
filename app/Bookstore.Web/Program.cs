using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Autofac;
using Autofac.Extensions.DependencyInjection;
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
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Targets;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Initialize the legacy static configuration wrapper from ASP.NET Core configuration
BookstoreConfiguration.Initialize(builder.Configuration);

// Load AWS configuration values into the configuration wrapper
await LoadAwsConfigurationAsync(builder.Configuration);

// Configure NLog
ConfigureLogging(builder.Configuration);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Add MVC with views
builder.Services.AddControllersWithViews();

// Configure Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Configure authentication
var authService = BookstoreConfiguration.GetSetting("Services/Authentication");
if (authService == "aws")
{
    ConfigureCognitoAuthentication(builder.Services, builder.Configuration);
}
else
{
    // Local auth: minimal cookie auth so middleware can set context.User
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
}

// Configure authorization - require authenticated user by default, allow anonymous with [AllowAnonymous]
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Use Autofac for dependency injection
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>((context, containerBuilder) =>
{
    ConfigureDependencyInjection(containerBuilder, context.Configuration);
});

var app = builder.Build();

// Seed the database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await BookstoreDbInitializer.SeedAsync(dbContext);
    }
    catch (Exception ex)
    {
        var logger = LogManager.GetCurrentClassLogger();
        logger.Error(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();

// Use local authentication middleware if not using Cognito
if (authService != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthorization();

// Map area routes (Admin area must come before default)
app.MapAreaControllerRoute(
    name: "Admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ─── Helper methods ───────────────────────────────────────────────────────────

static async Task LoadAwsConfigurationAsync(IConfiguration configuration)
{
    var rootPath = "/" + Constants.AppName;

    if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParameterRequest { Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection" };
        var response = await client.GetParameterAsync(request);
        BookstoreConfiguration.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest { Path = $"{rootPath}/Authentication/", Recursive = true };
        var response = await client.GetParametersByPathAsync(request);
        foreach (var parameter in response.Parameters)
        {
            BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
        }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest { Path = $"{rootPath}/Files/", Recursive = true };
        var response = await client.GetParametersByPathAsync(request);
        foreach (var parameter in response.Parameters)
        {
            BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
        }
    }
}

static void ConfigureLogging(IConfiguration configuration)
{
    var config = new LoggingConfiguration();

    NLog.Targets.Target loggingTarget;

    if (BookstoreConfiguration.GetSetting("Services/LoggingService") == "aws")
    {
        loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
    }
    else
    {
        loggingTarget = new NLog.Targets.DebuggerTarget();
    }

    config.AddTarget("bookstore", loggingTarget);
    config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));

    LogManager.Configuration = config;
}

static void ConfigureCognitoAuthentication(IServiceCollection services, IConfiguration configuration)
{
    services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
        options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.UseTokenLifetime = false;
        options.SaveTokens = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "cognito:username",
            RoleClaimType = "cognito:groups"
        };
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                var returnUrl = context.Request.GetReturnUrl();
                context.ProtocolMessage.RedirectUri = returnUrl;
                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = context =>
            {
                context.TokenEndpointRequest.RedirectUri = context.Request.GetReturnUrl();
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                var identity = (System.Security.Claims.ClaimsIdentity)context.Principal.Identity;

                var dto = new CreateOrUpdateCustomerDto(
                    identity.GetSub(),
                    identity.Name,
                    identity.FindFirst(y => y.Type.Contains("givenname"))?.Value ?? string.Empty,
                    identity.FindFirst(y => y.Type.Contains("surname"))?.Value ?? string.Empty);

                await service.CreateOrUpdateCustomerAsync(dto);
            }
        };
    });
}

static void ConfigureDependencyInjection(ContainerBuilder builder, IConfiguration configuration)
{
    builder.RegisterType<BookService>().As<IBookService>();
    builder.RegisterType<OrderService>().As<IOrderService>();
    builder.RegisterType<ReferenceDataService>().As<IReferenceDataService>();
    builder.RegisterType<OfferService>().As<IOfferService>();
    builder.RegisterType<CustomerService>().As<ICustomerService>();
    builder.RegisterType<AddressService>().As<IAddressService>();
    builder.RegisterType<ShoppingCartService>().As<IShoppingCartService>();
    builder.RegisterType<ImageResizeService>().As<IImageResizeService>();

    builder.RegisterType<CustomerRepository>().As<ICustomerRepository>().InstancePerLifetimeScope();
    builder.RegisterType<AddressRepository>().As<IAddressRepository>().InstancePerLifetimeScope();
    builder.RegisterType<BookRepository>().As<IBookRepository>().InstancePerLifetimeScope();
    builder.RegisterType<OfferRepository>().As<IOfferRepository>().InstancePerLifetimeScope();
    builder.RegisterType<ShoppingCartRepository>().As<IShoppingCartRepository>().InstancePerLifetimeScope();
    builder.RegisterType<OrderRepository>().As<IOrderRepository>().InstancePerLifetimeScope();
    builder.RegisterType<ReferenceDataRepository>().As<IReferenceDataRepository>().InstancePerLifetimeScope();

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        builder.RegisterType<AmazonS3Client>().As<IAmazonS3>();
        builder.RegisterType<S3FileService>().As<IFileService>();
    }
    else
    {
        // Get the web root path for storing local files
        var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!Directory.Exists(webRootPath))
            webRootPath = AppContext.BaseDirectory;

        builder.RegisterInstance(new LocalFileService(webRootPath)).As<IFileService>();
    }

    if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
    {
        builder.RegisterType<AmazonRekognitionClient>().As<IAmazonRekognition>();
        builder.RegisterType<RekognitionImageValidationService>().As<IImageValidationService>();
    }
    else
    {
        builder.RegisterType<LocalImageValidationService>().As<IImageValidationService>();
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
    {
        // Register LocalAuthenticationMiddleware as IMiddleware so ASP.NET Core can inject it
        builder.RegisterType<LocalAuthenticationMiddleware>().AsSelf().InstancePerLifetimeScope();
    }
}
