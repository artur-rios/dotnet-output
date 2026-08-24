using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ArturRios.Output;

/// <summary>
/// Provides helpers to paginate an <see cref="IQueryable{T}"/> either
/// synchronously or asynchronously. These extensions return a <see cref="PaginatedOutput{T}"/>.
/// </summary>
public static class PaginatedOutputExtensions
{
    /// <summary>
    /// Asynchronously paginates the query and returns a <see cref="PaginatedOutput{T}"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageNumber">1-based page number. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="pageSize">Number of items per page. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="orderBy">Optional ordering expression.</param>
    /// <param name="totalCount">
    /// Optional total count of items in the query. When supplied the count query is skipped and this value is
    /// reported as <see cref="PaginatedOutput{T}.TotalItems"/>. Negative values are clamped to <c>0</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a populated <see cref="PaginatedOutput{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
    public static async Task<PaginatedOutput<T>> PaginateAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        Expression<Func<T, object?>>? orderBy = null,
        int? totalCount = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);

        if (orderBy is not null)
        {
            query = OrderByExpression(query, orderBy);
        }

        var countWasSupplied = totalCount.HasValue;

        totalCount = countWasSupplied
            ? Math.Max(0, totalCount!.Value)
            : query.Provider is IAsyncQueryProvider
                ? await query.CountAsync(cancellationToken).ConfigureAwait(false)
                : query.Count();

        List<T> items;

        if (!countWasSupplied && totalCount.Value == 0)
        {
            items = [];
        }
        else
        {
            var pageQuery = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);

            items = pageQuery.Provider is IAsyncQueryProvider
                ? await pageQuery.ToListAsync(cancellationToken).ConfigureAwait(false)
                : pageQuery.ToList();
        }

        return PaginatedOutput<T>.New
            .WithData(items)
            .WithPagination(pageNumber, pageSize, totalCount.Value);
    }

    /// <summary>
    /// Synchronously paginates the query and returns a <see cref="PaginatedOutput{T}"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageNumber">1-based page number. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="pageSize">Number of items per page. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="orderBy">Optional ordering expression.</param>
    /// <param name="totalCount">
    /// Optional total count of items in the query. When supplied the count query is skipped and this value is
    /// reported as <see cref="PaginatedOutput{T}.TotalItems"/>. Negative values are clamped to <c>0</c>.
    /// </param>
    /// <returns>A populated <see cref="PaginatedOutput{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
    public static PaginatedOutput<T> Paginate<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        Expression<Func<T, object?>>? orderBy = null,
        int? totalCount = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);

        if (orderBy is not null)
        {
            query = OrderByExpression(query, orderBy);
        }

        var countWasSupplied = totalCount.HasValue;

        totalCount = countWasSupplied ? Math.Max(0, totalCount!.Value) : query.Count();

        var items = !countWasSupplied && totalCount.Value == 0
            ? []
            : query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

        return PaginatedOutput<T>.New
            .WithData(items)
            .WithPagination(pageNumber, pageSize, totalCount.Value);
    }

    /// <summary>
    /// Builds an <c>OrderBy</c> call dynamically from a lambda expression when
    /// the expression's return type is <see cref="object"/> and EF Core's
    /// expression translation would otherwise lose the typed delegate.
    /// </summary>
    private static IQueryable<T> OrderByExpression<T>(IQueryable<T> source, LambdaExpression keySelector)
    {
        var body = keySelector.Body;

        if (body is UnaryExpression unary && body.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        var keyType = body.Type;
        var parameter = keySelector.Parameters[0];

        var delegateType = typeof(Func<,>).MakeGenericType(typeof(T), keyType);
        var typedLambda = Expression.Lambda(delegateType, body, parameter);

        var orderByMethod = typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), keyType);

        return (IQueryable<T>)orderByMethod.Invoke(null, [source, typedLambda])!;
    }
}
