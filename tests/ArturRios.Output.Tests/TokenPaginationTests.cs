using System.Linq.Expressions;
using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

/// <summary>
/// Token pagination over in-memory LINQ. The EF Core side — SQL translation and parameterisation — is covered by
/// <c>Functional.EfCoreTokenPaginationTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public class TokenPaginationTests
{
    private static readonly byte[] SigningKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private static readonly PageTokenOptions Options = new() { SigningKey = SigningKey };

    private static List<Item> Items(int count) => Enumerable.Range(1, count).Select(i => new Item { Id = i, Value = $"v{i:D2}" }).ToList();

    private static List<PaginatedOutput<Item>> WalkAll(IQueryable<Item> query, int pageSize,
        SortDirection direction = SortDirection.Ascending, bool includeTotalCount = false)
    {
        var pages = new List<PaginatedOutput<Item>>();
        string? token = null;

        do
        {
            var page = query.PaginateByToken(token, pageSize, x => x.Id, Options, direction, includeTotalCount);

            Assert.True(page.Success, string.Join(", ", page.Errors));
            pages.Add(page);
            token = page.NextPageToken;
        } while (token is not null && pages.Count < 100);

        return pages;
    }

    private static void AssertRejected(PaginatedOutput<Item> page, string error)
    {
        Assert.False(page.Success);
        Assert.Equal([error], page.Errors);
        Assert.NotNull(page.Data);
        Assert.Empty(page.Data);
        Assert.Null(page.NextPageToken);
    }

    private static string SecondPageToken(IQueryable<Item> query, int pageSize = 10, PageTokenOptions? options = null)
    {
        var token = query.PaginateByToken(null, pageSize, x => x.Id, options ?? Options).NextPageToken;

        Assert.NotNull(token);

        return token;
    }

    [Fact]
    public void GivenNoToken_WhenPaginatingByToken_ThenTheFirstPageAndATokenComeBack()
    {
        var page = Items(23).AsQueryable().PaginateByToken(null, 10, x => x.Id, Options);

        Assert.True(page.Success);
        Assert.Equal(Enumerable.Range(1, 10), page.Data!.Select(x => x.Id));
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(0, page.TotalItems);
        Assert.Equal(0, page.TotalPages);
        Assert.True(page.HasNextPage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenABlankToken_WhenPaginatingByToken_ThenTheFirstPageComesBack(string token)
    {
        var page = Items(23).AsQueryable().PaginateByToken(token, 10, x => x.Id, Options);

        Assert.True(page.Success);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(Enumerable.Range(1, 10), page.Data!.Select(x => x.Id));
    }

    [Fact]
    public void GivenEachPagesToken_WhenWalkingTheListing_ThenEveryItemComesBackOnceInOrder()
    {
        var pages = WalkAll(Items(23).AsQueryable(), 10);

        Assert.Equal([1, 2, 3], pages.Select(x => x.PageNumber));
        Assert.Equal([10, 10, 3], pages.Select(x => x.Data!.Count));
        Assert.Equal(Enumerable.Range(1, 23), pages.SelectMany(x => x.Data!).Select(x => x.Id));
        Assert.Equal([true, true, false], pages.Select(x => x.HasNextPage));
    }

    [Fact]
    public void GivenADescendingDirection_WhenWalkingTheListing_ThenItemsComeBackLargestKeyFirst()
    {
        var pages = WalkAll(Items(23).AsQueryable(), 10, SortDirection.Descending);

        Assert.Equal(Enumerable.Range(1, 23).Reverse(), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Fact]
    public void GivenAnExactMultipleOfThePageSize_WhenWalkingTheListing_ThenTheLastFullPageHasNoToken()
    {
        var pages = WalkAll(Items(20).AsQueryable(), 10);

        Assert.Equal(2, pages.Count);
        Assert.Equal(10, pages[1].Data!.Count);
        Assert.Null(pages[1].NextPageToken);
    }

    [Fact]
    public void GivenAnEmptySource_WhenPaginatingByToken_ThenAnEmptySuccessfulFirstPageComesBack()
    {
        var page = new List<Item>().AsQueryable().PaginateByToken(null, 10, x => x.Id, Options);

        Assert.True(page.Success);
        Assert.NotNull(page.Data);
        Assert.Empty(page.Data);
        Assert.Equal(1, page.PageNumber);
        Assert.Null(page.NextPageToken);
    }

    [Fact]
    public void GivenAnUnorderedOrDifferentlyOrderedSource_WhenPaginatingByToken_ThenTheKeyDecidesTheOrder()
    {
        var shuffled = Items(23).OrderBy(x => (x.Id * 7) % 23).AsQueryable().OrderByDescending(x => x.Value);

        var pages = WalkAll(shuffled, 10);

        Assert.Equal(Enumerable.Range(1, 23), pages.SelectMany(x => x.Data!).Select(x => x.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GivenAPageSizeBelowOne_WhenPaginatingByToken_ThenItIsClampedToOne(int pageSize)
    {
        var page = Items(3).AsQueryable().PaginateByToken(null, pageSize, x => x.Id, Options);

        Assert.Equal(1, page.PageSize);
        Assert.Equal([1], page.Data!.Select(x => x.Id));
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public void GivenIncludeTotalCount_WhenWalkingTheListing_ThenEveryPageReportsTheWholeListingsCount()
    {
        var pages = WalkAll(Items(23).AsQueryable(), 10, includeTotalCount: true);

        Assert.All(pages, page =>
        {
            Assert.Equal(23, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
        });
    }

    [Fact]
    public void GivenDuplicateKeysAndATieBreaker_WhenWalkingTheListing_ThenNoRowIsDroppedOrRepeated()
    {
        // Values repeat in runs of three, so most page boundaries fall inside a run of equal keys.
        var items = Enumerable.Range(1, 23).Select(i => new Item { Id = i, Value = $"v{(i - 1) / 3}" }).Reverse().ToList();
        var query = items.AsQueryable();
        var seen = new List<Item>();
        string? token = null;

        do
        {
            var page = query.PaginateByToken(token, 2, x => x.Value, x => x.Id, Options);

            Assert.True(page.Success, string.Join(", ", page.Errors));
            seen.AddRange(page.Data!);
            token = page.NextPageToken;
        } while (token is not null && seen.Count < 100);

        Assert.Equal(Enumerable.Range(1, 23), seen.Select(x => x.Id));
    }

    [Fact]
    public void GivenDuplicateKeysAndATieBreaker_WhenWalkingDescending_ThenNoRowIsDroppedOrRepeated()
    {
        var query = Enumerable.Range(1, 23).Select(i => new Item { Id = i, Value = $"v{(i - 1) / 3}" }).AsQueryable();
        var seen = new List<Item>();
        string? token = null;

        do
        {
            var page = query.PaginateByToken(token, 4, x => x.Value, x => x.Id, Options, SortDirection.Descending);

            seen.AddRange(page.Data!);
            token = page.NextPageToken;
        } while (token is not null && seen.Count < 100);

        Assert.Equal(Enumerable.Range(1, 23).Reverse(), seen.Select(x => x.Id));
    }

    [Fact]
    public void GivenAStringKey_WhenWalkingTheListing_ThenItemsComeBackInKeyOrder()
    {
        var query = Items(23).OrderBy(x => x.Id % 5).AsQueryable();
        var seen = new List<Item>();
        string? token = null;

        do
        {
            var page = query.PaginateByToken(token, 5, x => x.Value, Options);

            seen.AddRange(page.Data!);
            token = page.NextPageToken;
        } while (token is not null && seen.Count < 100);

        Assert.Equal(Enumerable.Range(1, 23), seen.Select(x => x.Id));
    }

    [Fact]
    public void GivenRowsInsertedBeforeTheCursor_WhenFetchingTheNextPage_ThenItNeitherRepeatsNorSkips()
    {
        var items = Items(23);
        var token = SecondPageToken(items.AsQueryable());

        items.Insert(0, new Item { Id = -1 });
        items.Insert(0, new Item { Id = 0 });

        var page = items.AsQueryable().PaginateByToken(token, 10, x => x.Id, Options);

        Assert.Equal(Enumerable.Range(11, 10), page.Data!.Select(x => x.Id));
        Assert.Equal(2, page.PageNumber);
    }

    [Fact]
    public void GivenTheRowsAfterTheCursorWereDeleted_WhenFetchingTheNextPage_ThenAnEmptyLastPageComesBack()
    {
        var items = Items(23);
        var token = SecondPageToken(items.AsQueryable());

        items.RemoveAll(x => x.Id > 10);

        var page = items.AsQueryable().PaginateByToken(token, 10, x => x.Id, Options);

        Assert.True(page.Success);
        Assert.Empty(page.Data!);
        Assert.Equal(2, page.PageNumber);
        Assert.Null(page.NextPageToken);
    }

    [Fact]
    public void GivenAnEditedToken_WhenPaginatingByToken_ThenTheRequestIsRejectedAsInvalid()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query);

        var page = query.PaginateByToken((token[0] == 'A' ? "B" : "A") + token[1..], 10, x => x.Id, Options);

        AssertRejected(page, PageTokenErrors.Invalid);
    }

    [Fact]
    public void GivenATokenSignedWithAnotherKey_WhenPaginatingByToken_ThenTheRequestIsRejectedAsInvalid()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query, options: Options with { SigningKey = Enumerable.Repeat((byte)9, 32).ToArray() });

        AssertRejected(query.PaginateByToken(token, 10, x => x.Id, Options), PageTokenErrors.Invalid);
    }

    [Fact]
    public void GivenAnExpiredToken_WhenPaginatingByToken_ThenTheRequestIsRejectedAsExpired()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        var options = Options with { Lifetime = TimeSpan.FromMinutes(5), TimeProvider = clock };
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query, options: options);

        clock.Now = clock.Now.AddMinutes(6);

        AssertRejected(query.PaginateByToken(token, 10, x => x.Id, options), PageTokenErrors.Expired);
    }

    [Fact]
    public void GivenATokenFromAnotherPageSize_WhenPaginatingByToken_ThenTheRequestIsRejectedAsAMismatch()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query, pageSize: 2);

        AssertRejected(query.PaginateByToken(token, 1000, x => x.Id, Options), PageTokenErrors.Mismatch);
    }

    [Fact]
    public void GivenATokenFromAnotherScope_WhenPaginatingByToken_ThenTheRequestIsRejectedAsAMismatch()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query, options: Options with { Scope = "customer=1" });

        var page = query.PaginateByToken(token, 10, x => x.Id, Options with { Scope = "customer=2" });

        AssertRejected(page, PageTokenErrors.Mismatch);
    }

    [Fact]
    public void GivenATokenFromAnotherDirection_WhenPaginatingByToken_ThenTheRequestIsRejectedAsAMismatch()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query);

        AssertRejected(query.PaginateByToken(token, 10, x => x.Id, Options, SortDirection.Descending), PageTokenErrors.Mismatch);
    }

    [Fact]
    public void GivenATokenFromAnotherKey_WhenPaginatingByToken_ThenTheRequestIsRejectedAsAMismatch()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query);

        AssertRejected(query.PaginateByToken(token, 10, x => x.Value, Options), PageTokenErrors.Mismatch);
    }

    [Fact]
    public void GivenATokenIssuedWithoutATieBreaker_WhenPaginatingWithOne_ThenTheRequestIsRejectedAsAMismatch()
    {
        var query = Items(23).AsQueryable();
        var token = SecondPageToken(query);

        AssertRejected(query.PaginateByToken(token, 10, x => x.Id, x => x.Value, Options), PageTokenErrors.Mismatch);
    }

    [Fact]
    public void GivenANullKeyOnThePageBoundary_WhenPaginatingByToken_ThenAnInvalidOperationExceptionIsThrown()
    {
        var items = new List<Item> { new() { Id = 1, Value = null! }, new() { Id = 2, Value = "b" } };

        Assert.Throws<InvalidOperationException>(() =>
            items.AsQueryable().PaginateByToken(null, 1, x => x.Value, Options));
    }

    [Fact]
    public void GivenInvalidArguments_WhenPaginatingByToken_ThenTheyAreRejected()
    {
        var query = Items(3).AsQueryable();
        Expression<Func<Item, int>> key = x => x.Id;

        Assert.Throws<ArgumentNullException>(() => ((IQueryable<Item>)null!).PaginateByToken(null, 10, key, Options));
        Assert.Throws<ArgumentNullException>(() => query.PaginateByToken(null, 10, (Expression<Func<Item, int>>)null!, Options));
        Assert.Throws<ArgumentNullException>(() => query.PaginateByToken(null, 10, key, null!));
        Assert.Throws<ArgumentNullException>(() => query.PaginateByToken(null, 10, key, (Expression<Func<Item, int>>)null!, Options));
        Assert.Throws<ArgumentException>(() => query.PaginateByToken(null, 10, key, Options with { SigningKey = new byte[16] }));
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenPaginatingByTokenAsync_ThenTheyAreRejected()
    {
        var query = Items(3).AsQueryable();
        Expression<Func<Item, int>> key = x => x.Id;

        await Assert.ThrowsAsync<ArgumentNullException>(() => ((IQueryable<Item>)null!).PaginateByTokenAsync(null, 10, key, Options));
        await Assert.ThrowsAsync<ArgumentNullException>(() => query.PaginateByTokenAsync(null, 10, key, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => query.PaginateByTokenAsync(null, 10, key, Options with { SigningKey = [] }));
    }

    [Fact]
    public async Task GivenAnInMemorySource_WhenWalkingTheListingAsync_ThenEveryItemComesBackOnceInOrder()
    {
        var query = Items(23).AsQueryable();
        var seen = new List<Item>();
        var pageNumbers = new List<int>();
        string? token = null;

        do
        {
            var page = await query.PaginateByTokenAsync(token, 10, x => x.Id, Options, includeTotalCount: true);

            Assert.Equal(23, page.TotalItems);
            seen.AddRange(page.Data!);
            pageNumbers.Add(page.PageNumber);
            token = page.NextPageToken;
        } while (token is not null && seen.Count < 100);

        Assert.Equal([1, 2, 3], pageNumbers);
        Assert.Equal(Enumerable.Range(1, 23), seen.Select(x => x.Id));
    }

    [Fact]
    public async Task GivenATieBreaker_WhenWalkingTheListingAsync_ThenEveryItemComesBackOnceInOrder()
    {
        var query = Enumerable.Range(1, 9).Select(i => new Item { Id = i, Value = "same" }).AsQueryable();
        var seen = new List<Item>();
        string? token = null;

        do
        {
            var page = await query.PaginateByTokenAsync(token, 2, x => x.Value, x => x.Id, Options);

            seen.AddRange(page.Data!);
            token = page.NextPageToken;
        } while (token is not null && seen.Count < 100);

        Assert.Equal(Enumerable.Range(1, 9), seen.Select(x => x.Id));
    }

    [Fact]
    public async Task GivenAnEditedToken_WhenPaginatingByTokenAsync_ThenTheRequestIsRejectedAsInvalid()
    {
        var page = await Items(3).AsQueryable().PaginateByTokenAsync("not.a-token", 10, x => x.Id, Options);

        AssertRejected(page, PageTokenErrors.Invalid);
    }
}
