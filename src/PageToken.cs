using System.Buffers.Text;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArturRios.Output;

/// <summary>
/// What a page token carries: where the previous page ended and which page the token leads to.
/// </summary>
internal sealed class PageTokenPayload
{
    [JsonPropertyName("v")]
    public int Version { get; init; }

    /// <summary>The last returned item's key.</summary>
    [JsonPropertyName("k")]
    public JsonElement Key { get; init; }

    /// <summary>The last returned item's tiebreaker, when the listing has one.</summary>
    [JsonPropertyName("t")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? TieBreaker { get; init; }

    /// <summary>The page number the token leads to.</summary>
    [JsonPropertyName("p")]
    public int PageNumber { get; init; }

    /// <summary>Issue time, in Unix seconds.</summary>
    [JsonPropertyName("iat")]
    public long IssuedAt { get; init; }

    /// <summary>Hash of the request the token was issued for; see <see cref="PageToken.CreateFingerprint"/>.</summary>
    [JsonPropertyName("f")]
    public string Fingerprint { get; init; } = string.Empty;
}

/// <summary>
/// Issues and checks page tokens: <c>base64url(payload JSON) "." base64url(HMAC-SHA256(key, payload))</c>.
/// </summary>
/// <remarks>
/// Tokens are signed, not encrypted: anyone can base64-decode the payload and read the key values in it. The
/// signature is what stops a caller from editing the page number or key to skip ahead.
/// </remarks>
internal static class PageToken
{
    internal const int CurrentVersion = 1;

    private const int FingerprintLength = 16;

    /// <summary>
    /// Throws when the options cannot sign a token: a programmer error, as opposed to a bad token, which a caller
    /// supplies.
    /// </summary>
    internal static void ValidateOptions(PageTokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.SigningKey is null || options.SigningKey.Length < PageTokenOptions.MinimumSigningKeyLength)
        {
            throw new ArgumentException(
                $"The page token signing key must be at least {PageTokenOptions.MinimumSigningKeyLength} bytes long.",
                nameof(options));
        }
    }

    /// <summary>
    /// Hashes everything a token is only valid for — the key and tiebreaker selectors, the direction, the page size
    /// and the scope — so a token cannot be replayed against another listing. Only a truncated hash goes into the
    /// token, so the scope is never readable from it.
    /// </summary>
    internal static string CreateFingerprint(PageTokenOptions options, LambdaExpression keySelector,
        LambdaExpression? tieBreakerSelector, SortDirection direction, int pageSize)
    {
        // A JSON array keeps the parts apart, so no two different requests serialize to the same bytes.
        var parts = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            keySelector.ToString(),
            tieBreakerSelector?.ToString(),
            direction.ToString(),
            pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            options.Scope
        });

        return Base64Url.EncodeToString(SHA256.HashData(parts).AsSpan(0, FingerprintLength));
    }

    internal static string Create(PageTokenOptions options, string fingerprint, int pageNumber, JsonElement key,
        JsonElement? tieBreaker)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new PageTokenPayload
        {
            Version = CurrentVersion,
            Key = key,
            TieBreaker = tieBreaker,
            PageNumber = pageNumber,
            IssuedAt = options.TimeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Fingerprint = fingerprint
        });

        return Base64Url.EncodeToString(payload) + "." +
               Base64Url.EncodeToString(HMACSHA256.HashData(options.SigningKey, payload));
    }

    /// <summary>
    /// Checks a token and, when it is valid for this request, returns its payload. Otherwise <paramref name="error"/>
    /// is one of the <see cref="PageTokenErrors"/> constants.
    /// </summary>
    internal static bool TryRead(string token, PageTokenOptions options, string fingerprint,
        out PageTokenPayload? payload, out string? error)
    {
        payload = null;
        error = PageTokenErrors.Invalid;

        var dot = token.IndexOf('.');

        if (dot <= 0 || dot == token.Length - 1 || dot != token.LastIndexOf('.'))
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signature;

        try
        {
            payloadBytes = Base64Url.DecodeFromChars(token.AsSpan(0, dot));
            signature = Base64Url.DecodeFromChars(token.AsSpan(dot + 1));
        }
        catch (FormatException)
        {
            return false;
        }

        // Nothing is parsed until the signature checks out.
        if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(options.SigningKey, payloadBytes), signature))
        {
            return false;
        }

        PageTokenPayload? read;

        try
        {
            read = JsonSerializer.Deserialize<PageTokenPayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return false;
        }

        if (read is null || read.Version != CurrentVersion)
        {
            return false;
        }

        if (options.Lifetime is { } lifetime &&
            DateTimeOffset.FromUnixTimeSeconds(read.IssuedAt) + lifetime < options.TimeProvider.GetUtcNow())
        {
            error = PageTokenErrors.Expired;

            return false;
        }

        if (read.Fingerprint != fingerprint)
        {
            error = PageTokenErrors.Mismatch;

            return false;
        }

        payload = read;
        error = null;

        return true;
    }
}
