namespace ArturRios.Output;

/// <summary>
/// Represents a paginated collection result and pagination metadata.
/// </summary>
/// <typeparam name="T">Type of the items in the paginated collection.</typeparam>
public class PaginatedOutput<T> : DataOutput<List<T>>
{
    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Number of items in the current page.
    /// </summary>
    public int PageSize => Data?.Count ?? 0;

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Total number of pages computed from <see cref="TotalItems"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>
    /// Creates a new <see cref="PaginatedOutput{T}"/> instance.
    /// </summary>
    public static new PaginatedOutput<T> New => new();

    /// <summary>
    /// Sets pagination metadata for the current instance.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="totalItems">The total number of items available.</param>
    /// <returns>The same <see cref="PaginatedOutput{T}"/> instance for chaining.</returns>
    public PaginatedOutput<T> WithPagination(int pageNumber, int totalItems)
    {
        PageNumber = pageNumber;
        TotalItems = totalItems;

        return this;
    }

    /// <summary>
    /// Adds an item to the internal data list, creating the list if necessary.
    /// </summary>
    /// <param name="item">Item to add.</param>
    public void AddItem(T item)
    {
        Data ??= [];

        Data.Add(item);
    }

    /// <summary>
    /// Adds multiple items to the internal data list, creating the list if necessary.
    /// </summary>
    /// <param name="items">Items to add.</param>
    public void AddItems(IEnumerable<T> items)
    {
        Data ??= [];

        Data.AddRange(items);
    }

    /// <summary>
    /// Replaces the internal data list with the provided list and returns the instance.
    /// </summary>
    /// <param name="data">The list to set.</param>
    /// <returns>The same <see cref="PaginatedOutput{T}"/> instance for chaining.</returns>
    public new PaginatedOutput<T> WithData(List<T> data)
    {
        Data = data;

        return this;
    }

    /// <summary>
    /// Adds a single item to the data list and returns the instance.
    /// </summary>
    /// <param name="data">The item to add.</param>
    public PaginatedOutput<T> WithData(T data)
    {
        Data ??= [];

        Data.Add(data);

        return this;
    }

    /// <summary>
    /// Sets the internal data list to an empty list and returns the instance.
    /// </summary>
    /// <returns>The same <see cref="PaginatedOutput{T}"/> instance for chaining.</returns>
    public PaginatedOutput<T> WithEmptyData()
    {
        Data = [];

        return this;
    }

    /// <summary>
    /// Fluent helper to add an error on the current instance.
    /// </summary>
    /// <param name="error">Error message to add.</param>
    public new PaginatedOutput<T> WithError(string error)
    {
        AddError(error);

        return this;
    }

    /// <summary>
    /// Fluent helper to add multiple errors on the current instance.
    /// </summary>
    /// <param name="errors">Errors to add.</param>
    public new PaginatedOutput<T> WithErrors(IEnumerable<string> errors)
    {
        AddErrors(errors);

        return this;
    }

    /// <summary>
    /// Fluent helper to add an informational message on the current instance.
    /// </summary>
    /// <param name="message">Message to add.</param>
    public new PaginatedOutput<T> WithMessage(string message)
    {
        AddMessage(message);

        return this;
    }

    /// <summary>
    /// Fluent helper to add multiple informational messages on the current instance.
    /// </summary>
    /// <param name="messages">Messages to add.</param>
    public new PaginatedOutput<T> WithMessages(IEnumerable<string> messages)
    {
        AddMessages(messages);

        return this;
    }
}
