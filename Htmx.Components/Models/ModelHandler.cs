using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Input;
using Htmx.Components.Models.Table;
using Htmx.Components.Models.Builders;

namespace Htmx.Components.Models;

public abstract class ModelHandler
{
    public string TypeId { get; set; } = null!;
    public Type ModelType { get; protected set; } = null!;
    public Type KeyType { get; protected set; } = null!;
    public CrudFeatures CrudFeatures { get; internal set; }
    public ModelUI ModelUI { get; set; }
}

public enum ModelUI
{
    Table,
    Form,
}

public class ModelHandler<T, TKey> : ModelHandler
    where T : class
{
    private Expression<Func<T, TKey>> _keySelectorExpression = null!;
    private Func<T, TKey> _keySelectorFunc = null!;

    public ModelHandler()
    {
        ModelType = typeof(T);
        KeyType = typeof(TKey);
    }

    public Expression<Func<T, TKey>> KeySelector
    {
        get => _keySelectorExpression;
        set
        {
            _keySelectorExpression = value;
            _keySelectorFunc = value.CompileFast();
        }
    }

    public Func<IQueryable<T>>? GetQueryable { get; internal set; }
    public Func<T, Task<Result>>? CreateModel { get; internal set; }
    public Func<T, Task<Result>>? UpdateModel { get; internal set; }
    public Func<TKey, Task<Result>>? DeleteModel { get; internal set; }
    public Func<ActionModel>? GetCreateActionModel { get; internal set; }
    public Func<ActionModel>? GetUpdateActionModel { get; internal set; }
    public Func<ActionModel>? GetCancelActionModel { get; internal set; }
    public Func<ActionModel>? GetDeleteActionModel { get; internal set; }
    public Func<T, TKey> KeySelectorFunc => _keySelectorFunc;
    internal Dictionary<string, Func<IInputModel>>? InputModelBuilders { get; set; }
    internal Action<TableModelBuilder<T, TKey>>? ConfigureTableModel { get; set; }
    internal TableViewPaths Paths { get; set; } = null!;
    internal IServiceProvider ServiceProvider { get; set; } = null!;

    public Task<TableModel<T, TKey>> BuildTableModel()
    {
        var tableModelBuilder = new TableModelBuilder<T, TKey>(_keySelectorExpression, Paths, this, ServiceProvider);
        ConfigureTableModel?.Invoke(tableModelBuilder);
        return tableModelBuilder.Build();
    }

    public IInputModel BuildInputModel(string name)
    {
        if (InputModelBuilders == null || !InputModelBuilders.TryGetValue(name, out var builder))
            throw new ArgumentException($"No input model found for name '{name}'.");

        return builder();
    }

    /// <summary>
    /// Creates a predicate expression for the key selector. This is used to filter a collection
    /// to a single item based on the key. The key can be a simple value type, a string, or a
    /// composite type (e.g., a tuple or a class).
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public Expression<Func<T, bool>> GetKeyPredicate(TKey key)
    {
        var param = KeySelector.Parameters[0];
        Expression body;

        if (typeof(TKey).IsValueType || typeof(TKey) == typeof(string))
        {
            // Simple key: x => KeySelector(x) == key
            body = Expression.Equal(KeySelector.Body, Expression.Constant(key, typeof(TKey)));
        }
        else if (typeof(TKey).IsGenericType && typeof(TKey).Name.StartsWith("ValueTuple"))
        {
            // Composite key: compare each field
            var comparisons = new List<Expression>();
            var fields = typeof(TKey).GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                var memberAccess = Expression.Field(KeySelector.Body, fields[i]);
                var keyValue = Expression.Constant(fields[i].GetValue(key), fields[i].FieldType);
                comparisons.Add(Expression.Equal(memberAccess, keyValue));
            }
            body = comparisons.Aggregate(Expression.AndAlso);
        }
        else if (typeof(TKey).IsClass)
        {
            // Composite key: compare each property
            var comparisons = new List<Expression>();
            var properties = typeof(TKey).GetProperties();
            foreach (var prop in properties)
            {
                var memberAccess = Expression.Property(KeySelector.Body, prop);
                var keyValue = Expression.Constant(prop.GetValue(key), prop.PropertyType);
                comparisons.Add(Expression.Equal(memberAccess, keyValue));
            }
            if (comparisons.Count == 0)
                throw new NotSupportedException($"Key type {typeof(TKey)} has no properties.");
            body = comparisons.Aggregate(Expression.AndAlso);
        }
        else
        {
            throw new NotSupportedException($"Key type {typeof(TKey)} is not supported.");
        }

        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}

[Flags]
public enum CrudFeatures
{
    None = 0,
    Create = 1,
    Read = 2,
    Update = 4,
    Delete = 8
}
