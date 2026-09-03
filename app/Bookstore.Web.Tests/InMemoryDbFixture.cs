using Bookstore.Data;
using Bookstore.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Web.Tests;

/// <summary>
/// Shared in-memory database fixture for repository tests.
/// Each test class that needs a clean DB creates its own instance.
/// </summary>
public sealed class InMemoryDbFixture : IDisposable
{
    public ApplicationDbContext Context { get; }

    public InMemoryDbFixture(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose() => Context.Dispose();

    // ── Seed helpers ────────────────────────────────────────────────────────

    public ReferenceDataItem AddReferenceDataItem(
        ReferenceDataType type = ReferenceDataType.Genre,
        string text = "Fiction")
    {
        var item = new ReferenceDataItem(type, text);
        Context.ReferenceData.Add(item);
        Context.SaveChanges();
        return item;
    }
}
