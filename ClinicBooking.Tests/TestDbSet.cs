using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace ClinicBooking.Tests;

/// <summary>
/// In-memory DbSet so handler tests can call FirstOrDefaultAsync without SQL.
/// </summary>
internal sealed class TestDbSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>
    where T : class
{
    private readonly List<T> _data;
    private readonly IQueryProvider _provider;

    /// <summary>Wrap a List so LINQ in handlers hits memory, not SQL.</summary>
    public TestDbSet(List<T> data)
    {
        _data = data;
        _provider = new TestAsyncQueryProvider<T>(data.AsQueryable().Provider);
    }

    public override IEntityType EntityType => throw new NotSupportedException();

    public override LocalView<T> Local => throw new NotSupportedException();

    /// <summary>Handler calls Appointments.Add — we just append to the list. Entry is unused in tests.</summary>
    public override EntityEntry<T> Add(T entity)
    {
        _data.Add(entity);
        return null!;
    }

    public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(_data.GetEnumerator());

    public Type ElementType => typeof(T);

    public Expression Expression => _data.AsQueryable().Expression;

    public IQueryProvider Provider => _provider;
}

/// <summary>Makes FirstOrDefaultAsync run synchronously against the in-memory list.</summary>
internal sealed class TestAsyncQueryProvider<T> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression) => new TestQueryable<T>(this, expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new TestQueryable<TElement>(new TestAsyncQueryProvider<TElement>(_inner), expression);

    public object? Execute(Expression expression) => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var execute = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(resultType)
            .Invoke(_inner, [expression]);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [execute])!;
    }
}

/// <summary>IQueryable wrapper returned by the async provider.</summary>
internal sealed class TestQueryable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly IQueryProvider _provider;
    private readonly Expression _expression;

    public TestQueryable(IQueryProvider provider, Expression expression)
    {
        _provider = provider;
        _expression = expression;
    }

    public Type ElementType => typeof(T);
    public Expression Expression => _expression;
    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator() => _provider.Execute<IEnumerable<T>>(_expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(GetEnumerator());
}

/// <summary>Adapter so await foreach / FirstOrDefaultAsync can walk the list.</summary>
internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
}
