namespace ArturRios.Output;

/// <summary>
/// Base class for custom exceptions that carry multiple messages.
/// </summary>
/// <param name="messages">An array of messages that describe the error(s).</param>
public abstract class CustomException(string[] messages) : Exception(string.Join(", ", messages))
{
    /// <summary>
    /// Gets the messages associated with this exception.
    /// </summary>
    public string[] Messages { get; } = messages;
}
