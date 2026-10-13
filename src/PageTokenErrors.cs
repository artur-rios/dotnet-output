namespace ArturRios.Output;

/// <summary>
/// Errors token pagination reports, through <see cref="ProcessOutput.Errors"/>, when it rejects a page token.
/// They are constants so a caller can map them to a response (for example HTTP 400) without matching ad hoc strings.
/// </summary>
public static class PageTokenErrors
{
    /// <summary>
    /// The token is malformed, carries an unknown format version, or its signature does not match its content —
    /// which is also what an edited or forged token looks like.
    /// </summary>
    public const string Invalid = "The page token is invalid.";

    /// <summary>
    /// The token is older than <see cref="PageTokenOptions.Lifetime"/>.
    /// </summary>
    public const string Expired = "The page token has expired.";

    /// <summary>
    /// The token was issued for a different listing: another <see cref="PageTokenOptions.Scope"/>, key, tiebreaker,
    /// sort direction or page size.
    /// </summary>
    public const string Mismatch = "The page token does not match this query.";
}
