namespace ArturRios.Output;

/// <summary>
/// Represents the outcome of an operation and provides a standardized way to
/// collect messages, errors and a timestamp.
/// </summary>
public class ProcessOutput
{
    /// <summary>
    /// Collection of informational messages produced during the process.
    /// </summary>
    public List<string> Messages { get; } = [];

    /// <summary>
    /// Collection of error messages produced during the process. If this list
    /// is empty the <see cref="Success"/> property evaluates to <c>true</c>.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// UTC timestamp representing when this output instance was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether the process completed successfully (no errors were added).
    /// </summary>
    public bool Success => Errors.Count == 0;

    /// <summary>
    /// Creates a new <see cref="ProcessOutput"/> instance.
    /// </summary>
    public static ProcessOutput New => new();

    /// <summary>
    /// Adds a non-empty error message to the output.
    /// </summary>
    /// <param name="error">The error message to add.</param>
    public void AddError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return;
        }

        Errors.Add(error);
    }

    /// <summary>
    /// Adds multiple error messages to the output, ignoring empty or whitespace entries.
    /// </summary>
    /// <param name="errors">The collection of errors to add.</param>
    public void AddErrors(IEnumerable<string> errors) =>
        Errors.AddRange(errors.Where(e => !string.IsNullOrWhiteSpace(e)).ToList());


    /// <summary>
    /// Adds a non-empty informational message to the output.
    /// </summary>
    /// <param name="message">The message to add.</param>
    public void AddMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Messages.Add(message);
    }

    /// <summary>
    /// Adds multiple informational messages to the output, ignoring empty or whitespace entries.
    /// </summary>
    /// <param name="messages">The collection of messages to add.</param>
    public void AddMessages(IEnumerable<string> messages) =>
        Messages.AddRange(messages.Where(e => !string.IsNullOrWhiteSpace(e)).ToList());

    /// <summary>
    /// Fluent helper to add a single error and return the same instance.
    /// </summary>
    /// <param name="error">The error to add.</param>
    /// <returns>The same <see cref="ProcessOutput"/> instance for chaining.</returns>
    public ProcessOutput WithError(string error)
    {
        AddError(error);

        return this;
    }

    /// <summary>
    /// Fluent helper to add multiple errors and return the same instance.
    /// </summary>
    /// <param name="errors">The errors to add.</param>
    /// <returns>The same <see cref="ProcessOutput"/> instance for chaining.</returns>
    public ProcessOutput WithErrors(IEnumerable<string> errors)
    {
        AddErrors(errors);

        return this;
    }

    /// <summary>
    /// Fluent helper to add a single informational message and return the same instance.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <returns>The same <see cref="ProcessOutput"/> instance for chaining.</returns>
    public ProcessOutput WithMessage(string message)
    {
        AddMessage(message);

        return this;
    }

    /// <summary>
    /// Fluent helper to add multiple informational messages and return the same instance.
    /// </summary>
    /// <param name="messages">The messages to add.</param>
    /// <returns>The same <see cref="ProcessOutput"/> instance for chaining.</returns>
    public ProcessOutput WithMessages(IEnumerable<string> messages)
    {
        AddMessages(messages);

        return this;
    }
}
