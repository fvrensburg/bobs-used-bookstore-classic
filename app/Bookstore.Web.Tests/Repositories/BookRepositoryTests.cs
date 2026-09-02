using Bookstore.Data.Repositories;
using Bookstore.Domain.Books;
using Bookstore.Domain.ReferenceData;
using Xunit;

namespace Bookstore.Web.Tests.Repositories;

public class BookRepositoryTests : IDisposable
{
    private readonly InMemoryDbFixture _db = new();
    private readonly BookRepository _repo;

    public BookRepositoryTests()
    {
        _repo = new BookRepository(_db.Context);
        SeedReferenceData();
    }

    public void Dispose() => _db.Dispose();

    private void SeedReferenceData()
    {
        _db.Context.ReferenceData.AddRange(
            new ReferenceDataItem(ReferenceDataType.Publisher, "Publisher A") { Id = 1 },
            new ReferenceDataItem(ReferenceDataType.BookType, "Hardcover")    { Id = 2 },
            new ReferenceDataItem(ReferenceDataType.Genre,    "Fiction")      { Id = 3 },
            new ReferenceDataItem(ReferenceDataType.Condition, "New")         { Id = 4 }
        );
        _db.Context.SaveChanges();
    }

    private Book MakeBook(string name = "Test Book", int quantity = 5) =>
        new Book(name, "Author A", "ISBN-001", 1, 2, 3, 4, 9.99m, quantity);

    [Fact]
    public async Task AddAsync_SaveChanges_PersistsBook()
    {
        var book = MakeBook();

        await ((IBookRepository)_repo).AddAsync(book);
        await ((IBookRepository)_repo).SaveChangesAsync();

        var saved = await ((IBookRepository)_repo).GetAsync(book.Id);
        Assert.NotNull(saved);
        Assert.Equal("Test Book", saved.Name);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectCounts()
    {
        _db.Context.Book.AddRange(
            MakeBook("In Stock",    quantity: 10),
            MakeBook("Low Stock",   quantity: 3),   // <= LowBookThreshold (5)
            MakeBook("Out of Stock",quantity: 0)
        );
        _db.Context.SaveChanges();

        var stats = await ((IBookRepository)_repo).GetStatisticsAsync();

        Assert.NotNull(stats);
        Assert.Equal(3, stats.StockTotal);
        Assert.Equal(1, stats.OutOfStock);
        Assert.Equal(1, stats.LowStock);
    }

    [Fact]
    public async Task ListAsync_SearchString_FiltersResults()
    {
        _db.Context.Book.AddRange(
            MakeBook("The Hobbit"),
            MakeBook("Lord of the Rings")
        );
        _db.Context.SaveChanges();

        var results = await ((IBookRepository)_repo).ListAsync(
            searchString: "Hobbit", sortBy: "Name", pageIndex: 1, pageSize: 10);

        Assert.Single(results);
        Assert.Equal("The Hobbit", results.First().Name);
    }

    [Fact]
    public async Task ListAsync_Filters_ByCondition()
    {
        _db.Context.Book.AddRange(
            MakeBook("Book A"),
            MakeBook("Book B")
        );
        _db.Context.SaveChanges();

        var filters = new BookFilters { ConditionId = 4 };
        var results = await ((IBookRepository)_repo).ListAsync(filters, pageIndex: 1, pageSize: 10);

        Assert.Equal(2, results.Count);
    }
}
