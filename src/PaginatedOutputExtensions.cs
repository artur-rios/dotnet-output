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
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="orderBy">Optional ordering expression.</param>
    /// <param name="totalCount">Optional total count of items in query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a populated <see cref="PaginatedOutput{T}"/>.</returns>
    public static async Task<PaginatedOutput<T>> PaginateAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        Expression<Func<T, object?>>? orderBy = null,
        int? totalCount = null,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);

        if (orderBy != null)
        {
            query = query.OrderBy(orderBy);
        }

        totalCount ??= query.Provider is IAsyncQueryProvider
            ? await query.CountAsync(cancellationToken).ConfigureAwait(false)
            : query.Count();

        var skip = (pageNumber - 1) * pageSize;
        var pageQuery = query.Skip(skip).Take(pageSize);

        List<T> items;

        if (pageQuery.Provider is IAsyncQueryProvider)
        {
            items = await pageQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            items = pageQuery.ToList();
        }

        return PaginatedOutput<T>.New
            .WithData(items)
            .WithPagination(pageNumber, totalCount.Value);
    }

    /// <summary>
    /// Synchronously paginates the query and returns a <see cref="PaginatedOutput{T}"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="orderBy">Optional ordering expression.</param>
    /// <param name="totalCount">Optional total count of items in query.</param>
    /// <returns>A populated <see cref="PaginatedOutput{T}"/>.</returns>
    public static PaginatedOutput<T> Paginate<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        Expression<Func<T, object?>>? orderBy = null,
        int? totalCount = null)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);

        if (orderBy is not null)
        {
            query = OrderByExpression(query, orderBy);
        }

        totalCount ??= query.Count();

        var items = totalCount == 0
            ? []
            : query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

        return PaginatedOutput<T>.New
            .WithData(items)
            .WithPagination(pageNumber, totalCount.Value);
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
