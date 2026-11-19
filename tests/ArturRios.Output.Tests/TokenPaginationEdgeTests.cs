using System.Linq.Expressions;
using System.Text.Json;
using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

/// <summary>
/// The branches of token pagination that a well-behaved listing never reaches: tokens that are signed and match the
/// query but whose values cannot resume it, keys that cannot be compared, and null tiebreakers on a page boundary.
/// The tokens are made with the library's own signing code, as a server that changed between two requests would.
/// </summary>
[Trait("Category", "Unit")]
public class TokenPaginationEdgeTests
{
    private static readonly PageTokenOptions Options = new() { SigningKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray() };

    private static IQueryable<Item> Items(int count) =>
        Enumerable.Range(1, count).Select(i => new Item { Id = i, Value = $"v{i:D2}" }).AsQueryable();

    /// <summary>
    /// A token the library would accept for this exact request, carrying whatever key and tiebreaker it is given.
    /// </summary>
    private static string SignedToken(LambdaExpression keySelector, LambdaExpression? tieBreakerSelector, int pageSize,
        object? key, object? tieBreaker = null, SortDirection direction = SortDirection.Ascending)
    {
        var fingerprint = PageToken.CreateFingerprint(Options, keySelector, tieBreakerSelector, direction, pageSize);

        return PageToken.Create(Options, fingerprint, 2, JsonSerializer.SerializeToElement(key),
            tieBreakerSelector is null && tieBreaker is null ? null : JsonSerializer.SerializeToElement(tieBreaker));
    }

    private static void AssertRejectedAsInvalid<T>(PaginatedOutput<T> page)
    {
        Assert.False(page.Success);
        Assert.Equal([PageTokenErrors.Invalid], page.Errors);
        Assert.NotNull(page.Data);
        Assert.Empty(page.Data);
        Assert.Null(page.NextPageToken);
    }

    [Fact]
    public void GivenASignedTokenWhoseKeyIsNotOfTheKeyType_WhenPaginatingByToken_ThenTheRequestIsRejectedAsInvalid()
    {
        // Signed, and for this very request, but its key is a string where the listing now keys by an int: a
        // property whose type changed between deployments, say.
        Expression<Func<Item, int>> keySelector = x => x.Id;

        var token = SignedToken(keySelector, null, 5, key: "not a number");

        AssertRejectedAsInvalid(Items(10).PaginateByToken(token, 5, keySelector, Options));
    }

    [Fact]
    public void GivenASignedTokenWithANullKey_WhenPaginatingByToken_ThenTheRequestIsRejectedAsInvalid()
    {
        Expression<Func<Item, string>> keySelector = x => x.Value;

        var token = SignedToken(keySelector, null, 5, key: null);

        AssertRejectedAsInvalid(Items(10).PaginateByToken(token, 5, keySelector, Options));
    }

    [Fact]
    public void GivenASignedTokenWithoutTheTieBreakerTheQueryUses_WhenPaginatingByToken_ThenTheRequestIsRejectedAsInvalid()
    {
        Expression<Func<Item, string>> keySelector = x => x.Value;
        Expression<Func<Item, int>> tieBreakerSelector = x => x.Id;

        var fingerprint = PageToken.CreateFingerprint(Options, keySelector, tieBreakerSelector, SortDirection.Ascending, 5);
        var token = PageToken.Create(Options, fingerprint, 2, JsonSerializer.SerializeToElement("v03"), tieBreaker: null);

        AssertRejectedAsInvalid(Items(10).PaginateByToken(token, 5, keySelector, tieBreakerSelector, Options));
    }

    [Fact]
    public async Task GivenASignedTokenWhoseTieBreakerIsNotOfItsType_WhenPaginatingByTokenAsync_ThenTheRequestIsRejectedAsInvalid()
    {
        Expression<Func<Item, string>> keySelector = x => x.Value;
        Expression<Func<Item, int>> tieBreakerSelector = x => x.Id;

        var token = SignedToken(keySelector, tieBreakerSelector, 5, key: "v03", tieBreaker: "three");

        AssertRejectedAsInvalid(await Items(10).PaginateByTokenAsync(token, 5, keySelector, tieBreakerSelector, Options,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GivenAnEditedToken_WhenPaginatingByTokenWithATieBreaker_ThenTheRequestIsRejectedAsInvalid()
    {
        var first = Items(10).PaginateByToken(null, 3, x => x.Value, x => x.Id, Options);

        Assert.NotNull(first.NextPageToken);

        var edited = "x" + first.NextPageToken[1..];

        AssertRejectedAsInvalid(Items(10).PaginateByToken(edited, 3, x => x.Value, x => x.Id, Options));
    }

    [Fact]
    public void GivenNullTieBreakerSelector_WhenPaginatingByToken_ThenAnArgumentNullExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Items(3).PaginateByToken<Item, int, string>(null, 2, x => x.Id, null!, Options));
    }

    [Fact]
    public async Task GivenNullTieBreakerSelector_WhenPaginatingByTokenAsync_ThenAnArgumentNullExceptionIsThrown()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Items(3).PaginateByTokenAsync<Item, int, string>(null, 2, x => x.Id, null!, Options,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GivenANullTieBreakerOnThePageBoundary_WhenPaginatingByToken_ThenAnInvalidOperationExceptionIsThrown()
    {
        var items = new[]
        {
            new NullableTieBreakerItem(1, "a"),
            new NullableTieBreakerItem(2, null),
            new NullableTieBreakerItem(3, "c")
        }.AsQueryable();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            items.PaginateByToken(null, 2, x => x.Key, x => x.TieBreaker, Options));

        Assert.Contains("tiebreaker", exception.Message);
    }

    [Fact]
    public void GivenASignedTokenForAKeyTypeThatCannotBeCompared_WhenPaginatingByToken_ThenAnInvalidOperationExceptionNamesTheType()
    {
        // Uri has neither a comparison operator nor CompareTo, so there is no way to express "after this key".
        Expression<Func<UriItem, Uri>> keySelector = x => x.Address;

        var token = SignedToken(keySelector, null, 1, key: new Uri("https://b.test/"));
        var items = new[] { new UriItem(new Uri("https://a.test/")) }.AsQueryable();

        var exception = Assert.Throws<InvalidOperationException>(() => items.PaginateByToken(token, 1, keySelector, Options));

        Assert.Contains(typeof(Uri).ToString(), exception.Message);
    }

    [Fact]
    public void GivenAKeyComparedThroughCompareTo_WhenWalkingTheListing_ThenEveryItemComesBackOnceInOrder()
    {
        // A key type with CompareTo and no comparison operator is compared through CompareTo.
        var items = Enumerable.Range(1, 7).Select(i => new ComparableItem(new Comparable(i))).AsQueryable();

        var seen = new List<int>();
        string? token = null;

        do
        {
            var page = items.PaginateByToken(token, 3, x => x.Key, Options);

            Assert.True(page.Success, string.Join(", ", page.Errors));
            seen.AddRange(page.Data!.Select(item => item.Key.Value));
            token = page.NextPageToken;
        } while (token is not null);

        Assert.Equal(Enumerable.Range(1, 7), seen);
    }

    private sealed record NullableTieBreakerItem(int Key, string? TieBreaker);

    private sealed record UriItem(Uri Address);

    private sealed record ComparableItem(Comparable Key);

    /// <summary>
    /// A key type with CompareTo and no comparison operators, which JSON can carry.
    /// </summary>
    public sealed class Comparable(int value) : IComparable<Comparable>
    {
        public int Value { get; init; } = value;

        public int CompareTo(Comparable? other) => other is null ? 1 : Value.CompareTo(other.Value);
    }
}
