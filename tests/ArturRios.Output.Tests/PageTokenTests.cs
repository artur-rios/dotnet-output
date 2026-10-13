using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

[Trait("Category", "Unit")]
public class PageTokenTests
{
    private const string Fingerprint = "fingerprint";

    private static readonly byte[] SigningKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static PageTokenOptions Options(TimeSpan? lifetime = null, TimeProvider? clock = null, byte[]? key = null) =>
        new() { SigningKey = key ?? SigningKey, Lifetime = lifetime, TimeProvider = clock ?? new FixedTimeProvider(IssuedAt) };

    private static string Issue(PageTokenOptions options, int pageNumber = 2, object? key = null, object? tieBreaker = null) =>
        PageToken.Create(options, Fingerprint, pageNumber, JsonSerializer.SerializeToElement(key ?? 42),
            tieBreaker is null ? null : JsonSerializer.SerializeToElement(tieBreaker));

    private static string? ReadError(string token, PageTokenOptions options, string fingerprint = Fingerprint)
    {
        Assert.False(PageToken.TryRead(token, options, fingerprint, out var payload, out var error));
        Assert.Null(payload);

        return error;
    }

    private static PageTokenPayload Read(string token, PageTokenOptions options)
    {
        Assert.True(PageToken.TryRead(token, options, Fingerprint, out var payload, out var error));
        Assert.Null(error);
        Assert.NotNull(payload);

        return payload;
    }

    private static void AssertKeyRoundTrips<TKey>(TKey key)
    {
        var options = Options();

        var payload = Read(PageToken.Create(options, Fingerprint, 2, JsonSerializer.SerializeToElement(key), null), options);

        Assert.Equal(key, payload.Key.Deserialize<TKey>());
    }

    [Fact]
    public void GivenAnIssuedToken_WhenReadingItBack_ThenThePayloadIsRestored()
    {
        var options = Options();

        var payload = Read(Issue(options, pageNumber: 7, key: 42), options);

        Assert.Equal(PageToken.CurrentVersion, payload.Version);
        Assert.Equal(42, payload.Key.GetInt32());
        Assert.Null(payload.TieBreaker);
        Assert.Equal(7, payload.PageNumber);
        Assert.Equal(IssuedAt.ToUnixTimeSeconds(), payload.IssuedAt);
        Assert.Equal(Fingerprint, payload.Fingerprint);
    }

    [Fact]
    public void GivenATieBreaker_WhenReadingTheTokenBack_ThenBothValuesAreRestored()
    {
        var options = Options();
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var payload = Read(Issue(options, key: createdAt, tieBreaker: 9), options);

        Assert.Equal(createdAt, payload.Key.Deserialize<DateTime>());
        Assert.NotNull(payload.TieBreaker);
        Assert.Equal(9, payload.TieBreaker.Value.GetInt32());
    }

    [Fact]
    public void GivenSupportedKeyTypes_WhenReadingTheTokenBack_ThenEachKeyIsRestored()
    {
        AssertKeyRoundTrips(int.MaxValue);
        AssertKeyRoundTrips(long.MinValue);
        AssertKeyRoundTrips("Person 01 / ünïcødé");
        AssertKeyRoundTrips(Guid.Parse("5d0c4d1e-7a57-4c1b-9a8f-6f1b9b6d2e10"));
        AssertKeyRoundTrips(new DateTime(2026, 10, 13, 12, 30, 15, 123, DateTimeKind.Utc));
        AssertKeyRoundTrips(new DateTimeOffset(2026, 10, 13, 12, 30, 15, TimeSpan.FromHours(-3)));
        AssertKeyRoundTrips(12.5m);
    }

    [Fact]
    public void GivenAnIssuedToken_WhenInspectingIt_ThenItIsUrlSafe()
    {
        var token = Issue(Options(), key: "a key with ??? and >>> that base64 would pad");

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        Assert.Single(token, c => c == '.');
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-dot-at-all")]
    [InlineData(".")]
    [InlineData("payload.")]
    [InlineData(".signature")]
    [InlineData("a.b.c")]
    [InlineData("!!!.###")]
    public void GivenAMalformedToken_WhenReadingIt_ThenItIsInvalid(string token)
    {
        Assert.Equal(PageTokenErrors.Invalid, ReadError(token, Options()));
    }

    [Fact]
    public void GivenAnEditedPayload_WhenReadingTheToken_ThenItIsInvalid()
    {
        var options = Options();
        var token = Issue(options);

        Assert.Equal(PageTokenErrors.Invalid, ReadError(FlipFirstChar(token), options));
    }

    [Fact]
    public void GivenAnEditedSignature_WhenReadingTheToken_ThenItIsInvalid()
    {
        var options = Options();
        var token = Issue(options);
        var dot = token.IndexOf('.');

        var edited = token[..(dot + 1)] + FlipFirstChar(token[(dot + 1)..]);

        Assert.Equal(PageTokenErrors.Invalid, ReadError(edited, options));
    }

    [Fact]
    public void GivenAForgedPageNumber_WhenReadingTheToken_ThenItIsInvalid()
    {
        // The skip-a-page attack: keep the signature, re-encode the payload with a later page and key.
        var options = Options();
        var token = Issue(options, pageNumber: 2, key: 10);
        var signature = token[(token.IndexOf('.') + 1)..];

        var forgedPayload = Encoding.UTF8.GetString(DecodeBase64Url(token[..token.IndexOf('.')]))
            .Replace("\"p\":2", "\"p\":500")
            .Replace("\"k\":10", "\"k\":5000");

        var forged = EncodeBase64Url(Encoding.UTF8.GetBytes(forgedPayload)) + "." + signature;

        Assert.Equal(PageTokenErrors.Invalid, ReadError(forged, options));
    }

    [Fact]
    public void GivenThePayloadOfOneTokenAndTheSignatureOfAnother_WhenReadingTheToken_ThenItIsInvalid()
    {
        var options = Options();
        var first = Issue(options, key: 1);
        var second = Issue(options, key: 2);

        var spliced = first[..first.IndexOf('.')] + second[second.IndexOf('.')..];

        Assert.Equal(PageTokenErrors.Invalid, ReadError(spliced, options));
    }

    [Fact]
    public void GivenATokenSignedWithAnotherKey_WhenReadingIt_ThenItIsInvalid()
    {
        var otherKey = Enumerable.Repeat((byte)7, 32).ToArray();
        var token = Issue(Options(key: otherKey));

        Assert.Equal(PageTokenErrors.Invalid, ReadError(token, Options()));
    }

    [Fact]
    public void GivenASignedPayloadWithAnUnknownVersion_WhenReadingTheToken_ThenItIsInvalid()
    {
        var token = Sign($$"""{"v":2,"k":1,"p":2,"iat":{{IssuedAt.ToUnixTimeSeconds()}},"f":"{{Fingerprint}}"}""");

        Assert.Equal(PageTokenErrors.Invalid, ReadError(token, Options()));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"v":"one"}""")]
    public void GivenASignedPayloadThatIsNotATokenPayload_WhenReadingTheToken_ThenItIsInvalid(string payload)
    {
        Assert.Equal(PageTokenErrors.Invalid, ReadError(Sign(payload), Options()));
    }

    [Fact]
    public void GivenALifetime_WhenReadingTheTokenAtTheEndOfIt_ThenItIsStillValid()
    {
        var clock = new FixedTimeProvider(IssuedAt);
        var options = Options(TimeSpan.FromMinutes(10), clock);
        var token = Issue(options);

        clock.Now = IssuedAt.AddMinutes(10);

        Read(token, options);
    }

    [Fact]
    public void GivenALifetime_WhenReadingTheTokenAfterItEnds_ThenItHasExpired()
    {
        var clock = new FixedTimeProvider(IssuedAt);
        var options = Options(TimeSpan.FromMinutes(10), clock);
        var token = Issue(options);

        clock.Now = IssuedAt.AddMinutes(10).AddSeconds(1);

        Assert.Equal(PageTokenErrors.Expired, ReadError(token, options));
    }

    [Fact]
    public void GivenNoLifetime_WhenReadingAnOldToken_ThenItIsStillValid()
    {
        var clock = new FixedTimeProvider(IssuedAt);
        var options = Options(lifetime: null, clock);
        var token = Issue(options);

        clock.Now = IssuedAt.AddYears(100);

        Read(token, options);
    }

    [Fact]
    public void GivenAnotherFingerprint_WhenReadingTheToken_ThenItDoesNotMatch()
    {
        var options = Options();

        Assert.Equal(PageTokenErrors.Mismatch, ReadError(Issue(options), options, "another fingerprint"));
    }

    [Fact]
    public void GivenTheSameRequest_WhenFingerprinting_ThenTheFingerprintIsStable()
    {
        Expression<Func<Item, int>> key = x => x.Id;

        var first = PageToken.CreateFingerprint(Options(), key, null, SortDirection.Ascending, 10);
        var second = PageToken.CreateFingerprint(Options(), key, null, SortDirection.Ascending, 10);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GivenAnyBoundInputChanges_WhenFingerprinting_ThenTheFingerprintChanges()
    {
        Expression<Func<Item, int>> id = x => x.Id;
        Expression<Func<Item, string>> value = x => x.Value;
        var scoped = Options() with { Scope = "customer=1" };

        var fingerprints = new[]
        {
            PageToken.CreateFingerprint(Options(), id, null, SortDirection.Ascending, 10),
            PageToken.CreateFingerprint(scoped, id, null, SortDirection.Ascending, 10),
            PageToken.CreateFingerprint(scoped with { Scope = "customer=2" }, id, null, SortDirection.Ascending, 10),
            PageToken.CreateFingerprint(Options(), value, null, SortDirection.Ascending, 10),
            PageToken.CreateFingerprint(Options(), value, id, SortDirection.Ascending, 10),
            PageToken.CreateFingerprint(Options(), id, null, SortDirection.Descending, 10),
            PageToken.CreateFingerprint(Options(), id, null, SortDirection.Ascending, 11)
        };

        Assert.Equal(fingerprints.Length, fingerprints.Distinct().Count());
    }

    [Fact]
    public void GivenAScope_WhenFingerprinting_ThenTheScopeIsNotReadableFromTheFingerprint()
    {
        Expression<Func<Item, int>> id = x => x.Id;

        var fingerprint = PageToken.CreateFingerprint(Options() with { Scope = "customer=1" }, id, null, SortDirection.Ascending, 10);

        Assert.DoesNotContain("customer", fingerprint);
    }

    [Fact]
    public void GivenAKeyOfAtLeast32Bytes_WhenValidatingTheOptions_ThenNothingIsThrown()
    {
        PageToken.ValidateOptions(Options());
    }

    [Fact]
    public void GivenAShortSigningKey_WhenValidatingTheOptions_ThenAnArgumentExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => PageToken.ValidateOptions(Options(key: new byte[31])));
    }

    [Fact]
    public void GivenANullSigningKey_WhenValidatingTheOptions_ThenAnArgumentExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => PageToken.ValidateOptions(new PageTokenOptions { SigningKey = null! }));
    }

    [Fact]
    public void GivenNullOptions_WhenValidatingTheOptions_ThenAnArgumentNullExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => PageToken.ValidateOptions(null!));
    }

    private static string Sign(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);

        return EncodeBase64Url(bytes) + "." + EncodeBase64Url(HMACSHA256.HashData(SigningKey, bytes));
    }

    private static string FlipFirstChar(string value) => (value[0] == 'A' ? 'B' : 'A') + value[1..];

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
