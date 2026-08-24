using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Output.Tests.Functional;

/// <summary>
/// Exercises the pagination extensions end to end against a real EF Core provider (SQLite in memory), so the
/// asynchronous path really goes through <c>IAsyncQueryProvider</c> and the ordering expression really has to be
/// translated to SQL.
/// </summary>
[Trait("Category", "Functional")]
public sealed class EfCorePaginationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PeopleContext _context;

    public EfCorePaginationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _context = new PeopleContext(new DbContextOptionsBuilder<PeopleContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _context.People.AddRange(Enumerable.Range(1, 23).Select(i => new Person
        {
            Id = i,
            Name = $"Person {i:D2}",
            Active = i % 2 == 1
        }));

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void GivenARelationalQuery_WhenPaginating_ThenThePageAndMetadataComeBackFromTheDatabase()
    {
        var result = _context.People.Paginate(3, 10, x => x.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(new[] { 21, 22, 23 }, result.Data.Select(x => x.Id));
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(23, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GivenARelationalQuery_WhenPaginatingAsync_ThenThePageAndMetadataComeBackFromTheDatabase()
    {
        var result = await _context.People.PaginateAsync(3, 10, x => x.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(new[] { 21, 22, 23 }, result.Data.Select(x => x.Id));
        Assert.Equal(23, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GivenAValueTypeOrderKey_WhenPaginatingAsync_ThenTheProviderTranslatesTheOrdering()
    {
        var result = await _context.People
            .Where(x => x.Active)
            .PaginateAsync(1, 5, x => x.Id);

        Assert.NotNull(result.Data);
        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, result.Data.Select(x => x.Id));
        Assert.Equal(12, result.TotalItems);
    }

    [Fact]
    public async Task GivenAStringOrderKey_WhenPaginatingAsync_ThenTheProviderTranslatesTheOrdering()
    {
        var result = await _context.People.PaginateAsync(1, 3, x => x.Name);

        Assert.NotNull(result.Data);
        Assert.Equal(new[] { "Person 01", "Person 02", "Person 03" }, result.Data.Select(x => x.Name));
    }

    [Fact]
    public async Task GivenAFilterThatMatchesNothing_WhenPaginatingAsync_ThenAnEmptySuccessfulPageComesBack()
    {
        var result = await _context.People
            .Where(x => x.Name == "nobody")
            .PaginateAsync(1, 10, x => x.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenPaginatingAsync_ThenTheOperationIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _context.People.PaginateAsync(1, 10, x => x.Id, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task GivenASuppliedTotalCount_WhenPaginatingAsync_ThenTheDatabaseIsNotCounted()
    {
        var result = await _context.People.PaginateAsync(1, 10, x => x.Id, totalCount: 100);

        Assert.Equal(100, result.TotalItems);
        Assert.Equal(10, result.TotalPages);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.Count);
    }

    private sealed class Person
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool Active { get; init; }
    }

    private sealed class PeopleContext(DbContextOptions<PeopleContext> options) : DbContext(options)
    {
        public DbSet<Person> People => Set<Person>();
    }
}
