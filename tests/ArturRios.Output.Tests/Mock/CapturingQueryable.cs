using System.Collections;
using System.Linq.Expressions;

namespace ArturRios.Output.Tests.Mock;

/// <summary>
/// Records the expression the pagination extensions hand to the query provider, so a test can assert on the
/// shape of the tree that a real database provider would have to translate.
/// </summary>
public sealed class CapturingQueryable<T> : IQueryable<T>
{
    private readonly IQueryable<T> _inner;
    private readonly ExpressionRecorder _recorder = new();

    public CapturingQueryable(IEnumerable<T> source) => _inner = source.AsQueryable();

    /// <summary>The first expression handed to the provider, or <c>null</c> when nothing was composed.</summary>
    public Expression? LastExpression => _recorder.Expression;

    public Type ElementType => typeof(T);

    public Expression Expression => _inner.Expression;

    public IQueryProvider Provider => new CapturingProvider(_inner.Provider, _recorder);

    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class ExpressionRecorder
    {
        public Expression? Expression { get; private set; }

        public void Record(Expression expression) => Expression ??= expression;
    }

    private sealed class CapturingProvider(IQueryProvider inner, ExpressionRecorder recorder) : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            recorder.Record(expression);

            return inner.CreateQuery(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            recorder.Record(expression);

            return inner.CreateQuery<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            recorder.Record(expression);

            return inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            recorder.Record(expression);

            return inner.Execute<TResult>(expression);
        }
    }
}
