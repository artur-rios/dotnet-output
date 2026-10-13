namespace ArturRios.Output.Tests.Mock;

/// <summary>
/// A clock that stays where it is put, so token issue and expiry times can be asserted exactly.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
