using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Models;
using Htmx.Components.Table;
using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.Services;

public interface IModelRegistry
{
    void Register<T, TKey>(string typeId, Action<IServiceProvider, ModelHandlerBuilder<T, TKey>> config)
        where T : class;
    ModelHandler? GetModelHandler(string typeId);
}

public class ModelRegistry : IModelRegistry
{
    private readonly Dictionary<string, ModelHandler> _modelHandlers = new();
    private readonly TableViewPaths _tableViewPaths;
    private readonly IServiceProvider _serviceProvider;

    public ModelRegistry(TableViewPaths tableViewPaths, IServiceProvider serviceProvider)
    {
        _tableViewPaths = tableViewPaths;
        _serviceProvider = serviceProvider;
    }


    public void Register<T, TKey>(string typeId, Action<IServiceProvider, ModelHandlerBuilder<T, TKey>> config)
        where T : class
    {
        var builder = new ModelHandlerBuilder<T, TKey>(typeId, _tableViewPaths);
        config.Invoke(_serviceProvider, builder);
        var handler = builder.Build();
        _modelHandlers[typeId] = handler;
    }

    public ModelHandler? GetModelHandler(string typeId)
    {
        return _modelHandlers.TryGetValue(typeId, out var handler) ? handler : null;
    }
}

public class ModelHandlerBuilder<T, TKey>
    where T : class
{
    internal ModelHandlerBuilder(string typeId, TableViewPaths tableViewPaths)
    {
        _typeId = typeId;
        _tableViewPaths = tableViewPaths;
    }

    private TableViewPaths _tableViewPaths;
    private string _typeId;
    Expression<Func<T, TKey>>? _keySelector;
    private Func<IQueryable<T>>? _getQueryable;
    private Func<T, Task<Result>>? _insertModel;
    private Func<T, Task<Result>>? _updateModel;
    private Func<TKey, Task<Result>>? _deleteModel;
    private Action<TableModelBuilder<T, TKey>>? _configureTableModel;

    public ModelHandlerBuilder<T, TKey> WithTypeId(string typeId)
    {
        _typeId = typeId;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithKeySelector(Expression<Func<T, TKey>> keySelector)
    {
        _keySelector = keySelector;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithQueryable(Func<IQueryable<T>> getQueryable)
    {
        _getQueryable = getQueryable;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithInsertModel(Func<T, Task<Result>> saveModel)
    {
        _insertModel = saveModel;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithUpdateModel(Func<T, Task<Result>> updateModel)
    {
        _updateModel = updateModel;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithDeleteModel(Func<TKey, Task<Result>> deleteModel)
    {
        _deleteModel = deleteModel;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithTableModel(Action<TableModelBuilder<T, TKey>> configure)
    {
        _configureTableModel = configure;
        return this;
    }

    public ModelHandler<T, TKey> Build()
    {
        return new ModelHandler<T, TKey>
        {
            TypeId = _typeId,
            KeySelector = _keySelector ?? throw new ArgumentNullException(nameof(_keySelector)),
            GetQueryable = _getQueryable,
            InsertModel = _insertModel,
            UpdateModel = _updateModel,
            DeleteModel = _deleteModel,
            BuildTableModel = _configureTableModel != null
                ? () =>
                    {
                        var tableModelBuilder = new TableModelBuilder<T, TKey>(_keySelector!, _tableViewPaths);
                        _configureTableModel?.Invoke(tableModelBuilder);
                        return tableModelBuilder.Build();
                    }
            : null,
        };
    }
}

public abstract class ModelHandler
{
    public required string TypeId { get; init; }
    public Type ModelType { get; protected set; } = null!;
    public Type KeyType { get; protected set; } = null!;
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

    public required Expression<Func<T, TKey>> KeySelector
    {
        get => _keySelectorExpression;
        set
        {
            _keySelectorExpression = value;
            _keySelectorFunc = value.CompileFast();
        }
    }

    public Func<IQueryable<T>>? GetQueryable { get; init; }
    public Func<T, Task<Result>>? InsertModel { get; init; }
    public Func<T, Task<Result>>? UpdateModel { get; init; }
    public Func<TKey, Task<Result>>? DeleteModel { get; init; }
    public Func<T, TKey> KeySelectorFunc => _keySelectorFunc;
    public Func<TableModel<T, TKey>>? BuildTableModel { get; init; }

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