using Bookstore.Data.Repositories;
using Bookstore.Domain.ReferenceData;
using Xunit;

namespace Bookstore.Web.Tests.Repositories;

public class ReferenceDataRepositoryTests : IDisposable
{
    private readonly InMemoryDbFixture _db = new();
    private readonly ReferenceDataRepository _repo;

    public ReferenceDataRepositoryTests()
    {
        _repo = new ReferenceDataRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AddAsync_PersistsItem()
    {
        var item = new ReferenceDataItem(ReferenceDataType.Genre, "Horror");

        await ((IReferenceDataRepository)_repo).AddAsync(item);
        await ((IReferenceDataRepository)_repo).SaveChangesAsync();

        var loaded = await ((IReferenceDataRepository)_repo).GetAsync(item.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Horror", loaded.Text);
    }

    [Fact]
    public async Task FullListAsync_ReturnsAllItems()
    {
        _db.AddReferenceDataItem(ReferenceDataType.Genre, "Fiction");
        _db.AddReferenceDataItem(ReferenceDataType.Genre, "Non-Fiction");
        _db.AddReferenceDataItem(ReferenceDataType.BookType, "Hardcover");

        var all = (await ((IReferenceDataRepository)_repo).FullListAsync()).ToList();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task ListAsync_FiltersCorrectly()
    {
        _db.AddReferenceDataItem(ReferenceDataType.Genre, "Fantasy");
        _db.AddReferenceDataItem(ReferenceDataType.BookType, "Paperback");

        var filters = new ReferenceDataFilters { ReferenceDataType = ReferenceDataType.Genre };
        var result = await ((IReferenceDataRepository)_repo).ListAsync(filters, pageIndex: 1, pageSize: 10);

        Assert.Single(result);
        Assert.Equal("Fantasy", result.First().Text);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForMissingId()
    {
        var result = await ((IReferenceDataRepository)_repo).GetAsync(99999);
        Assert.Null(result);
    }
}
