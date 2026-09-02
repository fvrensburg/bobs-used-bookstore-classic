using BobsBookstoreClassic.Data;
using Bookstore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Bookstore.Web.Tests.Controllers;

/// <summary>
/// Integration tests using WebApplicationFactory to exercise the real ASP.NET Core pipeline.
/// The application is configured to use an in-memory database and local auth so no AWS
/// connectivity is required.
/// </summary>
public class HomeControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomeControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Replace SQL Server DbContext with in-memory DB
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(opt =>
                    opt.UseInMemoryDatabase("IntegrationTests"));
            });
        });

        // Seed BookstoreConfiguration with test values so local service paths resolve
        BookstoreConfiguration.Initialize(new ConfigurationAdapter(new Dictionary<string, string?>
        {
            ["Services:Authentication"]       = "local",
            ["Services:Database"]             = "local",
            ["Services:FileService"]          = "local",
            ["Services:ImageValidationService"] = "local",
            ["Services:LoggingService"]       = "local",
            ["ConnectionStrings:BookstoreDatabaseConnection"] = "InMemory"
        }));
    }

    [Fact]
    public async Task Get_Privacy_ReturnsOk_WhenAuthenticated()
    {
        // The test client has the LocalAuthentication cookie set, which satisfies
        // the LocalAuthenticationMiddleware and sets a ClaimsPrincipal with the
        // Administrators role, satisfying the global AuthorizeFilter.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", "LocalAuthentication=1");

        var response = await client.GetAsync("/Home/Privacy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Home_Index_Returns2xxOrRedirect()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", "LocalAuthentication=1");

        var response = await client.GetAsync("/");

        Assert.True(
            (int)response.StatusCode < 400,
            $"Expected 2xx/3xx but got {response.StatusCode}");
    }
}

// ── Minimal IConfiguration adapter used to seed BookstoreConfiguration ──────

file sealed class ConfigurationAdapter : Microsoft.Extensions.Configuration.IConfiguration
{
    private readonly Dictionary<string, string?> _data;

    public ConfigurationAdapter(Dictionary<string, string?> data) => _data = data;

    public string? this[string key]
    {
        get => _data.TryGetValue(key, out var v) ? v : null;
        set => _data[key] = value;
    }

    public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
        Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();

    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
        new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

    public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) =>
        new EmptySection(key, _data.Where(kv => kv.Key.StartsWith(key + ":"))
            .ToDictionary(kv => kv.Key[(key.Length + 1)..], kv => kv.Value));

    private sealed class EmptySection : Microsoft.Extensions.Configuration.IConfigurationSection
    {
        private readonly Dictionary<string, string?> _data;
        public EmptySection(string path, Dictionary<string, string?> data) { Path = path; Key = path; _data = data; }
        public string Key { get; }
        public string Path { get; }
        public string? Value { get => _data.TryGetValue(string.Empty, out var v) ? v : null; set { } }
        public string? this[string key] { get => _data.TryGetValue(key, out var v) ? v : null; set { } }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
            Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) =>
            new EmptySection(key, new Dictionary<string, string?>());
    }
}
