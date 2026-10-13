using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArturRios.Output.Tests.Functional;

/// <summary>
/// Walks token pagination end to end against a real EF Core provider (SQLite in memory), so the keyset filter really
/// has to be translated to SQL — EF Core throws rather than evaluate an untranslatable filter on the client — and the
/// cursor really has to travel as a parameter.
/// </summary>
[Trait("Category", "Functional")]
public sealed class EfCoreTokenPaginationTests : IDisposable
{
    private static readonly PageTokenOptions Options = new() { SigningKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray() };

    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly OrdersContext _context;
    private readonly List<string> _commands = [];

    public EfCoreTokenPaginationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _context = new OrdersContext(new DbContextOptionsBuilder<OrdersContext>()
            .UseSqlite(_connection)
            .LogTo(_commands.Add, [RelationalEventId.CommandExecuted])
            .Options);

        _context.Database.EnsureCreated();

        // Inserted in reverse so the table's natural order is not the key order.
        _context.Orders.AddRange(Enumerable.Range(1, 23).Reverse().Select(i => new Order
        {
            Id = i,
            Name = $"Order {i:D2}",
            CreatedAt = Start.AddMinutes(i),
            Day = Start.AddDays((i - 1) / 3),
            ExternalId = new Guid(i, 0, 0, new byte[8]),
            Open = i % 2 == 1
        }));

        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        _commands.Clear();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<List<PaginatedOutput<Order>>> WalkAllAsync(
        Func<string?, Task<PaginatedOutput<Order>>> fetchPage)
    {
        var pages = new List<PaginatedOutput<Order>>();
        string? token = null;

        do
        {
            var page = await fetchPage(token);

            Assert.True(page.Success, string.Join(", ", page.Errors));
            pages.Add(page);
            token = page.NextPageToken;
        } while (token is not null && pages.Count < 100);

        return pages;
    }

    [Fact]
    public void GivenARelationalQuery_WhenWalkingByToken_ThenEveryRowComesBackOnceInKeyOrder()
    {
        var pages = new List<PaginatedOutput<Order>>();
        string? token = null;

        do
        {
            var page = _context.Orders.PaginateByToken(token, 10, x => x.Id, Options);

            pages.Add(page);
            token = page.NextPageToken;
        } while (token is not null && pages.Count < 100);

        Assert.Equal([1, 2, 3], pages.Select(x => x.PageNumber));
        Assert.Equal(Enumerable.Range(1, 23), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public async Task GivenARelationalQuery_WhenWalkingByTokenAsyncDescending_ThenEveryRowComesBackOnceLargestFirst()
    {
        var pages = await WalkAllAsync(token =>
            _context.Orders.PaginateByTokenAsync(token, 10, x => x.Id, Options, SortDirection.Descending));

        Assert.Equal(Enumerable.Range(1, 23).Reverse(), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public async Task GivenAStringKey_WhenWalkingByTokenAsync_ThenTheProviderTranslatesTheComparison()
    {
        var pages = await WalkAllAsync(token => _context.Orders.PaginateByTokenAsync(token, 4, x => x.Name, Options));

        Assert.Equal(Enumerable.Range(1, 23), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public async Task GivenADateTimeKey_WhenWalkingByTokenAsync_ThenTheProviderTranslatesTheComparison()
    {
        var pages = await WalkAllAsync(token => _context.Orders.PaginateByTokenAsync(token, 5, x => x.CreatedAt, Options));

        Assert.Equal(Enumerable.Range(1, 23), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public async Task GivenAGuidKey_WhenWalkingByTokenAsync_ThenRowsComeBackOnceInTheDatabasesGuidOrder()
    {
        var expected = await _context.Orders.OrderBy(x => x.ExternalId).Select(x => x.Id).ToListAsync();

        var pages = await WalkAllAsync(token => _context.Orders.PaginateByTokenAsync(token, 5, x => x.ExternalId, Options));

        Assert.Equal(expected, pages.SelectMany(x => x.Data!).Select(x => x.Id));
        Assert.Equal(23, expected.Distinct().Count());
    }

    [Fact]
    public async Task GivenARepeatingKeyAndATieBreaker_WhenWalkingByTokenAsync_ThenNoRowIsDroppedOrRepeated()
    {
        var pages = await WalkAllAsync(token =>
            _context.Orders.PaginateByTokenAsync(token, 2, x => x.Day, x => x.Id, Options));

        Assert.Equal(Enumerable.Range(1, 23), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public async Task GivenARepeatingKeyAndATieBreaker_WhenWalkingByTokenAsyncDescending_ThenNoRowIsDroppedOrRepeated()
    {
        var pages = await WalkAllAsync(token =>
            _context.Orders.PaginateByTokenAsync(token, 4, x => x.Day, x => x.Id, Options, SortDirection.Descending));

        Assert.Equal(Enumerable.Range(1, 23).Reverse(), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public async Task GivenAFilteredQuery_WhenWalkingByTokenAsyncWithTheTotal_ThenTheFilterAndTheCountHold()
    {
        var pages = await WalkAllAsync(token =>
            _context.Orders.Where(x => x.Open).PaginateByTokenAsync(token, 5, x => x.Id, Options, includeTotalCount: true));

        Assert.Equal(Enumerable.Range(1, 23).Where(i => i % 2 == 1), pages.SelectMany(x => x.Data!).Select(x => x.Id));
        Assert.All(pages, page =>
        {
            Assert.Equal(12, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
        });
    }

    [Fact]
    public async Task GivenTwoLaterPages_WhenFetchingThem_ThenBothRunTheSameParameterisedSql()
    {
        // Were the cursor inlined as a literal, every page would send different SQL and compile its own plan.
        var first = await _context.Orders.PaginateByTokenAsync(null, 5, x => x.Id, Options);
        var second = await _context.Orders.PaginateByTokenAsync(first.NextPageToken, 5, x => x.Id, Options);

        _commands.Clear();
        await _context.Orders.PaginateByTokenAsync(first.NextPageToken, 5, x => x.Id, Options);
        await _context.Orders.PaginateByTokenAsync(second.NextPageToken, 5, x => x.Id, Options);

        Assert.Equal(2, _commands.Count);
        Assert.Equal(SqlOf(_commands[0]), SqlOf(_commands[1]));
        Assert.Contains("WHERE", SqlOf(_commands[0]));
        Assert.Contains("LIMIT", SqlOf(_commands[0]));
        Assert.Contains("> @", SqlOf(_commands[0]));
    }

    [Fact]
    public async Task GivenARowInsertedBeforeTheCursor_WhenFetchingTheNextPage_ThenItNeitherRepeatsNorSkips()
    {
        var first = await _context.Orders.PaginateByTokenAsync(null, 10, x => x.Id, Options);

        _context.Orders.Add(new Order { Id = 0, Name = "Order 00", CreatedAt = Start, Day = Start, ExternalId = Guid.Empty });
        await _context.SaveChangesAsync();

        var second = await _context.Orders.PaginateByTokenAsync(first.NextPageToken, 10, x => x.Id, Options);

        Assert.Equal(Enumerable.Range(11, 10), second.Data!.Select(x => x.Id));
    }

    [Fact]
    public async Task GivenAnEditedToken_WhenPaginatingByTokenAsync_ThenItIsRejectedWithoutQueryingTheDatabase()
    {
        var first = await _context.Orders.PaginateByTokenAsync(null, 10, x => x.Id, Options);
        var token = first.NextPageToken!;

        _commands.Clear();
        var page = await _context.Orders.PaginateByTokenAsync((token[0] == 'A' ? "B" : "A") + token[1..], 10, x => x.Id, Options);

        Assert.False(page.Success);
        Assert.Equal([PageTokenErrors.Invalid], page.Errors);
        Assert.Empty(_commands);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenPaginatingByTokenAsync_ThenTheOperationIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _context.Orders.PaginateByTokenAsync(null, 10, x => x.Id, Options, cancellationToken: cancellation.Token));
    }

    /// <summary>The SQL text of a logged command, without the log line's prefix and parameter values.</summary>
    private static string SqlOf(string logLine) => logLine[logLine.IndexOf("SELECT", StringComparison.Ordinal)..];

    private sealed class Order
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime Day { get; init; }

        public Guid ExternalId { get; init; }

        public bool Open { get; init; }
    }

    private sealed class OrdersContext(DbContextOptions<OrdersContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
    }
}
