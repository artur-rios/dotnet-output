namespace ArturRios.Output;

/// <summary>
/// Settings for issuing and checking the signed page tokens used by token pagination. Build one instance — typically
/// a singleton from configuration — and pass it to every call that walks the same listing.
/// </summary>
public sealed record PageTokenOptions
{
    /// <summary>
    /// The minimum length, in bytes, of <see cref="SigningKey"/>.
    /// </summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>
    /// Secret HMAC-SHA256 key that signs every token, at least <see cref="MinimumSigningKeyLength"/> bytes long. A
    /// token signed with another key is rejected, so every instance serving the same listing must share it.
    /// </summary>
    public required byte[] SigningKey { get; init; }

    /// <summary>
    /// How long a token stays valid after it is issued, or <c>null</c> for tokens that never expire.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>
    /// Optional caller-defined string that binds tokens to one listing — for example <c>"orders:customer=42"</c> — so
    /// a token issued under one scope is rejected under another. It is hashed into the token, never stored in it.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Clock used to stamp tokens and check their <see cref="Lifetime"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
