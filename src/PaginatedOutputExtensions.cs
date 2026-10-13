using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
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

        if ((!countWasSupplied && totalCount.Value == 0) || !TryGetOffset(pageNumber, pageSize, out var offset))
        {
            items = [];
        }
        else
        {
            var pageQuery = query.Skip(offset).Take(pageSize);

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

        var items = (!countWasSupplied && totalCount.Value == 0) || !TryGetOffset(pageNumber, pageSize, out var offset)
            ? []
            : query
                .Skip(offset)
                .Take(pageSize)
                .ToList();

        return PaginatedOutput<T>.New
            .WithData(items)
            .WithPagination(pageNumber, pageSize, totalCount.Value);
    }

    /// <summary>
    /// Returns the page after the one that issued <paramref name="pageToken"/>, or the first page when no token is
    /// given, walking the query in <paramref name="keySelector"/> order with a keyset filter instead of an offset.
    /// </summary>
    /// <remarks>
    /// The key must be unique and non-null; for a key that repeats (a timestamp, a name) use the overload that takes
    /// a tiebreaker. Any ordering already on <paramref name="query"/> is replaced. Tokens are signed, so a caller can
    /// only reach a page with the token issued by the page before it, but not encrypted: the key values in a token
    /// are readable.
    /// </remarks>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <typeparam name="TKey">Type of the key the listing is ordered and resumed by.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageToken">
    /// <see cref="PaginatedOutput{T}.NextPageToken"/> from the previous page, or <c>null</c> (or blank) for the first
    /// page.
    /// </param>
    /// <param name="pageSize">Number of items per page. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="keySelector">The unique, non-null key the listing is ordered and resumed by.</param>
    /// <param name="options">Signing key, lifetime and scope of the tokens.</param>
    /// <param name="direction">Whether the listing walks the key ascending or descending.</param>
    /// <param name="includeTotalCount">
    /// When <c>true</c>, the whole query is counted and reported as <see cref="PaginatedOutput{T}.TotalItems"/>;
    /// otherwise the count query is skipped and <c>TotalItems</c> is <c>0</c>.
    /// </param>
    /// <returns>
    /// The page with its <see cref="PaginatedOutput{T}.NextPageToken"/>, or — when the token is invalid, expired or
    /// issued for another listing — an empty output carrying one of the <see cref="PageTokenErrors"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/>, <paramref name="keySelector"/> or <paramref name="options"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><see cref="PageTokenOptions.SigningKey"/> is too short.</exception>
    /// <exception cref="InvalidOperationException">The last item on a page that has a next page has a <c>null</c> key.</exception>
    public static PaginatedOutput<T> PaginateByToken<T, TKey>(
        this IQueryable<T> query,
        string? pageToken,
        int pageSize,
        Expression<Func<T, TKey>> keySelector,
        PageTokenOptions options,
        SortDirection direction = SortDirection.Ascending,
        bool includeTotalCount = false) =>
        TryPlanKeysetPage<T, TKey, object?>(query, pageToken, pageSize, keySelector, null, options, direction,
            out var plan, out var rejection)
            ? ReadKeysetPage(plan, includeTotalCount)
            : rejection;

    /// <summary>
    /// Returns the page after the one that issued <paramref name="pageToken"/>, or the first page when no token is
    /// given, walking the query in (<paramref name="keySelector"/>, <paramref name="tieBreakerSelector"/>) order with a
    /// keyset filter instead of an offset.
    /// </summary>
    /// <remarks>
    /// Use this overload when the key can repeat: the tiebreaker — typically the primary key — must make the pair
    /// unique, and neither may be <c>null</c>. Everything else is as in
    /// <see cref="PaginateByToken{T, TKey}(IQueryable{T}, string?, int, Expression{Func{T, TKey}}, PageTokenOptions, SortDirection, bool)"/>.
    /// </remarks>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <typeparam name="TKey">Type of the key the listing is ordered and resumed by.</typeparam>
    /// <typeparam name="TTieBreaker">Type of the tiebreaker that orders items with equal keys.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageToken">
    /// <see cref="PaginatedOutput{T}.NextPageToken"/> from the previous page, or <c>null</c> (or blank) for the first
    /// page.
    /// </param>
    /// <param name="pageSize">Number of items per page. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="keySelector">The non-null key the listing is ordered and resumed by.</param>
    /// <param name="tieBreakerSelector">The non-null value that orders items with equal keys.</param>
    /// <param name="options">Signing key, lifetime and scope of the tokens.</param>
    /// <param name="direction">Whether the listing walks the key and tiebreaker ascending or descending.</param>
    /// <param name="includeTotalCount">
    /// When <c>true</c>, the whole query is counted and reported as <see cref="PaginatedOutput{T}.TotalItems"/>;
    /// otherwise the count query is skipped and <c>TotalItems</c> is <c>0</c>.
    /// </param>
    /// <returns>
    /// The page with its <see cref="PaginatedOutput{T}.NextPageToken"/>, or — when the token is invalid, expired or
    /// issued for another listing — an empty output carrying one of the <see cref="PageTokenErrors"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/>, <paramref name="keySelector"/>, <paramref name="tieBreakerSelector"/> or
    /// <paramref name="options"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><see cref="PageTokenOptions.SigningKey"/> is too short.</exception>
    /// <exception cref="InvalidOperationException">
    /// The last item on a page that has a next page has a <c>null</c> key or tiebreaker.
    /// </exception>
    public static PaginatedOutput<T> PaginateByToken<T, TKey, TTieBreaker>(
        this IQueryable<T> query,
        string? pageToken,
        int pageSize,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TTieBreaker>> tieBreakerSelector,
        PageTokenOptions options,
        SortDirection direction = SortDirection.Ascending,
        bool includeTotalCount = false)
    {
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);

        return TryPlanKeysetPage(query, pageToken, pageSize, keySelector, tieBreakerSelector, options, direction,
            out var plan, out var rejection)
            ? ReadKeysetPage(plan, includeTotalCount)
            : rejection;
    }

    /// <summary>
    /// Asynchronously returns the page after the one that issued <paramref name="pageToken"/>, or the first page when
    /// no token is given. See
    /// <see cref="PaginateByToken{T, TKey}(IQueryable{T}, string?, int, Expression{Func{T, TKey}}, PageTokenOptions, SortDirection, bool)"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <typeparam name="TKey">Type of the key the listing is ordered and resumed by.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageToken">
    /// <see cref="PaginatedOutput{T}.NextPageToken"/> from the previous page, or <c>null</c> (or blank) for the first
    /// page.
    /// </param>
    /// <param name="pageSize">Number of items per page. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="keySelector">The unique, non-null key the listing is ordered and resumed by.</param>
    /// <param name="options">Signing key, lifetime and scope of the tokens.</param>
    /// <param name="direction">Whether the listing walks the key ascending or descending.</param>
    /// <param name="includeTotalCount">
    /// When <c>true</c>, the whole query is counted and reported as <see cref="PaginatedOutput{T}.TotalItems"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to the page, or to an empty output carrying one of the <see cref="PageTokenErrors"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/>, <paramref name="keySelector"/> or <paramref name="options"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><see cref="PageTokenOptions.SigningKey"/> is too short.</exception>
    /// <exception cref="InvalidOperationException">The last item on a page that has a next page has a <c>null</c> key.</exception>
    public static async Task<PaginatedOutput<T>> PaginateByTokenAsync<T, TKey>(
        this IQueryable<T> query,
        string? pageToken,
        int pageSize,
        Expression<Func<T, TKey>> keySelector,
        PageTokenOptions options,
        SortDirection direction = SortDirection.Ascending,
        bool includeTotalCount = false,
        CancellationToken cancellationToken = default) =>
        TryPlanKeysetPage<T, TKey, object?>(query, pageToken, pageSize, keySelector, null, options, direction,
            out var plan, out var rejection)
            ? await ReadKeysetPageAsync(plan, includeTotalCount, cancellationToken).ConfigureAwait(false)
            : rejection;

    /// <summary>
    /// Asynchronously returns the page after the one that issued <paramref name="pageToken"/>, or the first page when
    /// no token is given, ordered by key and tiebreaker. See
    /// <see cref="PaginateByToken{T, TKey, TTieBreaker}(IQueryable{T}, string?, int, Expression{Func{T, TKey}}, Expression{Func{T, TTieBreaker}}, PageTokenOptions, SortDirection, bool)"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the queryable.</typeparam>
    /// <typeparam name="TKey">Type of the key the listing is ordered and resumed by.</typeparam>
    /// <typeparam name="TTieBreaker">Type of the tiebreaker that orders items with equal keys.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="pageToken">
    /// <see cref="PaginatedOutput{T}.NextPageToken"/> from the previous page, or <c>null</c> (or blank) for the first
    /// page.
    /// </param>
    /// <param name="pageSize">Number of items per page. Values below <c>1</c> are clamped to <c>1</c>.</param>
    /// <param name="keySelector">The non-null key the listing is ordered and resumed by.</param>
    /// <param name="tieBreakerSelector">The non-null value that orders items with equal keys.</param>
    /// <param name="options">Signing key, lifetime and scope of the tokens.</param>
    /// <param name="direction">Whether the listing walks the key and tiebreaker ascending or descending.</param>
    /// <param name="includeTotalCount">
    /// When <c>true</c>, the whole query is counted and reported as <see cref="PaginatedOutput{T}.TotalItems"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to the page, or to an empty output carrying one of the <see cref="PageTokenErrors"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/>, <paramref name="keySelector"/>, <paramref name="tieBreakerSelector"/> or
    /// <paramref name="options"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><see cref="PageTokenOptions.SigningKey"/> is too short.</exception>
    /// <exception cref="InvalidOperationException">
    /// The last item on a page that has a next page has a <c>null</c> key or tiebreaker.
    /// </exception>
    public static async Task<PaginatedOutput<T>> PaginateByTokenAsync<T, TKey, TTieBreaker>(
        this IQueryable<T> query,
        string? pageToken,
        int pageSize,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TTieBreaker>> tieBreakerSelector,
        PageTokenOptions options,
        SortDirection direction = SortDirection.Ascending,
        bool includeTotalCount = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);

        return TryPlanKeysetPage(query, pageToken, pageSize, keySelector, tieBreakerSelector, options, direction,
            out var plan, out var rejection)
            ? await ReadKeysetPageAsync(plan, includeTotalCount, cancellationToken).ConfigureAwait(false)
            : rejection;
    }

    /// <summary>
    /// A token-paginated request, checked and composed but not yet run.
    /// </summary>
    /// <param name="CountQuery">The caller's query, counted when the total is requested.</param>
    /// <param name="PageQuery">The ordered, keyset-filtered query, fetching one row more than a page.</param>
    /// <param name="PageNumber">The page this request reads.</param>
    /// <param name="PageSize">The clamped page size.</param>
    /// <param name="IssueNextToken">Issues the token that resumes after the given item.</param>
    private sealed record KeysetPlan<T>(
        IQueryable<T> CountQuery,
        IQueryable<T> PageQuery,
        int PageNumber,
        int PageSize,
        Func<T, string> IssueNextToken);

    /// <summary>
    /// Checks the arguments and the token, then composes the page query. Returns <c>false</c> with an error output in
    /// <paramref name="rejection"/> when the token is rejected; throws for programmer errors.
    /// </summary>
    private static bool TryPlanKeysetPage<T, TKey, TTieBreaker>(
        IQueryable<T> query,
        string? pageToken,
        int pageSize,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TTieBreaker>>? tieBreakerSelector,
        PageTokenOptions options,
        SortDirection direction,
        [NotNullWhen(true)] out KeysetPlan<T>? plan,
        [NotNullWhen(false)] out PaginatedOutput<T>? rejection)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);
        PageToken.ValidateOptions(options);

        pageSize = Math.Max(1, pageSize);

        var descending = direction == SortDirection.Descending;
        var fingerprint = PageToken.CreateFingerprint(options, keySelector, tieBreakerSelector, direction, pageSize);
        var pageNumber = 1;
        var page = query;

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!PageToken.TryRead(pageToken, options, fingerprint, out var payload, out var error))
            {
                plan = null;
                rejection = PaginatedOutput<T>.New.WithEmptyData().WithError(error!);

                return false;
            }

            TTieBreaker? lastTieBreaker = default;

            // A signed payload whose values no longer read as the key types — a property whose type changed between
            // deployments, say — cannot resume the listing.
            if (!TryReadValue<TKey>(payload!.Key, out var lastKey) ||
                (tieBreakerSelector is not null && !TryReadValue(payload.TieBreaker, out lastTieBreaker)))
            {
                plan = null;
                rejection = PaginatedOutput<T>.New.WithEmptyData().WithError(PageTokenErrors.Invalid);

                return false;
            }

            page = page.Where(After(keySelector, lastKey, tieBreakerSelector, lastTieBreaker, descending));
            pageNumber = payload.PageNumber;
        }

        var ordered = descending ? page.OrderByDescending(keySelector) : page.OrderBy(keySelector);

        if (tieBreakerSelector is not null)
        {
            ordered = descending ? ordered.ThenByDescending(tieBreakerSelector) : ordered.ThenBy(tieBreakerSelector);
        }

        var getKey = keySelector.Compile();
        var getTieBreaker = tieBreakerSelector?.Compile();
        var nextPageNumber = pageNumber + 1;

        plan = new KeysetPlan<T>(query, ordered.Take(pageSize + 1), pageNumber, pageSize, IssueNextToken);
        rejection = null;

        return true;

        string IssueNextToken(T last)
        {
            var key = getKey(last) ?? throw new InvalidOperationException(
                "Token pagination needs a non-null key, but the last item on the page has a null key.");

            JsonElement? tieBreaker = null;

            if (getTieBreaker is not null)
            {
                tieBreaker = JsonSerializer.SerializeToElement(getTieBreaker(last) ??
                    throw new InvalidOperationException(
                        "Token pagination needs a non-null tiebreaker, but the last item on the page has a null one."));
            }

            return PageToken.Create(options, fingerprint, nextPageNumber, JsonSerializer.SerializeToElement(key),
                tieBreaker);
        }
    }

    private static PaginatedOutput<T> ReadKeysetPage<T>(KeysetPlan<T> plan, bool includeTotalCount)
    {
        int? totalItems = includeTotalCount ? plan.CountQuery.Count() : null;

        return CompleteKeysetPage(plan, plan.PageQuery.ToList(), totalItems);
    }

    private static async Task<PaginatedOutput<T>> ReadKeysetPageAsync<T>(KeysetPlan<T> plan, bool includeTotalCount,
        CancellationToken cancellationToken)
    {
        int? totalItems = null;

        if (includeTotalCount)
        {
            totalItems = plan.CountQuery.Provider is IAsyncQueryProvider
                ? await plan.CountQuery.CountAsync(cancellationToken).ConfigureAwait(false)
                : plan.CountQuery.Count();
        }

        var rows = plan.PageQuery.Provider is IAsyncQueryProvider
            ? await plan.PageQuery.ToListAsync(cancellationToken).ConfigureAwait(false)
            : plan.PageQuery.ToList();

        return CompleteKeysetPage(plan, rows, totalItems);
    }

    /// <summary>
    /// Drops the probe row fetched past the page and, when there was one, issues the token for the next page.
    /// </summary>
    private static PaginatedOutput<T> CompleteKeysetPage<T>(KeysetPlan<T> plan, List<T> rows, int? totalItems)
    {
        string? nextPageToken = null;

        if (rows.Count > plan.PageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            nextPageToken = plan.IssueNextToken(rows[^1]);
        }

        return PaginatedOutput<T>.New
            .WithData(rows)
            .WithPagination(plan.PageNumber, plan.PageSize, totalItems ?? 0)
            .WithNextPageToken(nextPageToken);
    }

    private static bool TryReadValue<TValue>(JsonElement? element, [NotNullWhen(true)] out TValue? value)
    {
        value = default;

        if (element is null)
        {
            return false;
        }

        try
        {
            value = element.Value.Deserialize<TValue>();
        }
        catch (JsonException)
        {
            return false;
        }

        return value is not null;
    }

    /// <summary>
    /// Builds the keyset filter: items past <paramref name="lastKey"/> in the walk direction, or — with a tiebreaker —
    /// items with the same key and a tiebreaker past <paramref name="lastTieBreaker"/>.
    /// </summary>
    private static Expression<Func<T, bool>> After<T, TKey, TTieBreaker>(
        Expression<Func<T, TKey>> keySelector,
        TKey lastKey,
        Expression<Func<T, TTieBreaker>>? tieBreakerSelector,
        TTieBreaker? lastTieBreaker,
        bool descending)
    {
        var parameter = keySelector.Parameters[0];
        var key = keySelector.Body;
        var capturedKey = Captured(lastKey, key.Type);

        var body = Beyond(key, capturedKey, descending);

        if (tieBreakerSelector is not null)
        {
            var tieBreaker = new ParameterReplacer(tieBreakerSelector.Parameters[0], parameter)
                .Visit(tieBreakerSelector.Body);

            body = Expression.OrElse(body, Expression.AndAlso(
                Expression.Equal(key, capturedKey),
                Beyond(tieBreaker, Captured(lastTieBreaker, tieBreaker.Type), descending)));
        }

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// <c>left &gt; right</c> (or <c>&lt;</c> when descending), through the type's comparison operator when it has
    /// one and through <c>left.CompareTo(right) &gt; 0</c> otherwise (strings, for example), which EF Core translates
    /// to a plain SQL comparison too.
    /// </summary>
    private static Expression Beyond(Expression left, Expression right, bool descending)
    {
        var comparison = descending ? ExpressionType.LessThan : ExpressionType.GreaterThan;

        if (HasComparisonOperator(left.Type))
        {
            return Expression.MakeBinary(comparison, left, right);
        }

        var compareTo = left.Type.GetMethod(nameof(IComparable.CompareTo), [left.Type])
                        ?? throw new InvalidOperationException(
                            $"Token pagination cannot compare keys of type {left.Type}: it has neither a comparison operator nor CompareTo({left.Type}).");

        return Expression.MakeBinary(comparison, Expression.Call(left, compareTo, right), Expression.Constant(0));
    }

    private static bool HasComparisonOperator(Type type) =>
        ComparisonOperatorSupport.GetOrAdd(type, static t =>
        {
            try
            {
                Expression.GreaterThan(Expression.Default(t), Expression.Default(t));

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });

    /// <summary>
    /// Whether each key type has a comparison operator, cached because finding out costs an exception for the types
    /// that do not.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> ComparisonOperatorSupport = new();

    /// <summary>
    /// Wraps a value the way the compiler wraps a captured variable — a field read off a constant — so EF Core sends
    /// it as a SQL parameter rather than inlining a literal, and every page shares one cached query plan.
    /// </summary>
    private static Expression Captured<TValue>(TValue value, Type type)
    {
        Expression field = Expression.Field(Expression.Constant(new CapturedValue<TValue>(value)),
            nameof(CapturedValue<TValue>.Value));

        return field.Type == type ? field : Expression.Convert(field, type);
    }

    private sealed class CapturedValue<TValue>(TValue value)
    {
        public readonly TValue Value = value;
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }

    /// <summary>
    /// Computes how many items precede the page, failing when that number does not fit in an
    /// <see cref="int"/>.
    /// </summary>
    /// <remarks>
    /// Computed in 64 bits because <c>(pageNumber - 1) * pageSize</c> overflows for a far-out page — both are
    /// caller input — and the wrapped value can be negative, which LINQ and SQLite both treat as no offset at
    /// all, handing back the first page under the far-out page number. An offset past
    /// <see cref="int.MaxValue"/> is past every item <c>Skip</c> can address, so that page is empty.
    /// </remarks>
    private static bool TryGetOffset(int pageNumber, int pageSize, out int offset)
    {
        var wideOffset = (long)(pageNumber - 1) * pageSize;

        offset = wideOffset <= int.MaxValue ? (int)wideOffset : 0;

        return wideOffset <= int.MaxValue;
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
