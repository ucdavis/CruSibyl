using System.Linq.Expressions;
using Htmx.Components.Authorization;
using Htmx.Components.Extensions;
using Htmx.Components.Input;
using Htmx.Components.Models;
using Htmx.Components.Models.Builders;
using Htmx.Components.Models.Table;
using Htmx.Components.Table;

namespace Htmx.Components.Services;

public interface IModelRegistry
{
    void Register<T, TKey>(string typeId, Action<IServiceProvider, ModelHandlerBuilder<T, TKey>> config)
        where T : class;
    Task<ModelHandler?> GetModelHandler(string typeId);
    Task<ModelHandler<T, TKey>?> GetModelHandler<T, TKey>(string typeId)
        where T : class;
}

public class ModelRegistry : IModelRegistry
{
    private readonly Dictionary<string, Task<ModelHandler>> _modelHandlers = new();
    private readonly TableViewPaths _tableViewPaths;
    private readonly IServiceProvider _serviceProvider;
    private readonly IResourceOperationRegistry _resourceOperationRegistry;

    public ModelRegistry(TableViewPaths tableViewPaths, IServiceProvider serviceProvider,
        IResourceOperationRegistry resourceOperationRegistry)
    {
        _tableViewPaths = tableViewPaths;
        _serviceProvider = serviceProvider;
        _resourceOperationRegistry = resourceOperationRegistry;
    }

    public void Register<T, TKey>(string typeId, Action<IServiceProvider, ModelHandlerBuilder<T, TKey>> config)
        where T : class
    {
        var builder = new ModelHandlerBuilder<T, TKey>(typeId, _tableViewPaths, _resourceOperationRegistry);
        config.Invoke(_serviceProvider, builder);
        var buildTask = builder.Build();
        _modelHandlers[typeId] = buildTask.ContinueWith(task => (ModelHandler)task.Result);
    }

    public async Task<ModelHandler?> GetModelHandler(string typeId)
    {
        var task = _modelHandlers.TryGetValue(typeId, out var handler) ? handler : null;
        if (task == null)
        {
            return null;
        }
        return await task;
    }

    public async Task<ModelHandler<T, TKey>?> GetModelHandler<T, TKey>(string typeId)
        where T : class
    {
        var handler = await GetModelHandler(typeId);
        if (handler is ModelHandler<T, TKey> typedHandler)
        {
            return typedHandler;
        }
        return null;
    }
}

public class ModelHandlerBuilder<T, TKey>
    where T : class
{
    internal ModelHandlerBuilder(string typeId, TableViewPaths tableViewPaths, IResourceOperationRegistry resourceOperationRegistry)
    {
        _resourceOperationRegistry = resourceOperationRegistry;
        _typeId = typeId;
        _tableViewPaths = tableViewPaths;
    }

    private readonly IResourceOperationRegistry _resourceOperationRegistry;
    private TableViewPaths _tableViewPaths;
    private string _typeId;
    Expression<Func<T, TKey>>? _keySelector;
    private Func<IQueryable<T>>? _getQueryable;
    private Func<T, Task<Result>>? _createModel;
    private Func<T, Task<Result>>? _updateModel;
    private Func<TKey, Task<Result>>? _deleteModel;
    private Action<TableModelBuilder<T, TKey>>? _configureTableModel;
    private readonly Dictionary<string, Func<IInputModel>> _inputModelBuilders = new();

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

    public ModelHandlerBuilder<T, TKey> WithCreateModel(Func<T, Task<Result>> createModel)
    {
        _createModel = createModel;
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

    public ModelHandlerBuilder<T, TKey> WithInputModel<TProp>(Expression<Func<T, TProp>> propertySelector,
        Action<InputModelBuilder<T, TProp>> configure)
    {
        var propName = propertySelector.GetPropertyName();
        var builder = new InputModelBuilder<T, TProp>(propertySelector);
        _inputModelBuilders[propName] = () =>
        {
            configure(builder);
            var inputModel = builder.Build();
            return inputModel;
        };
        return this;
    }

    public async Task<ModelHandler<T, TKey>> Build()
    {
        if (_getQueryable != null)
        {
            await _resourceOperationRegistry.Register(_typeId, Constants.Authorization.Operations.Read);
        }
        if (_createModel != null)
        {
            await _resourceOperationRegistry.Register(_typeId, Constants.Authorization.Operations.Create);
        }
        if (_updateModel != null)
        {
            await _resourceOperationRegistry.Register(_typeId, Constants.Authorization.Operations.Update);
        }
        if (_deleteModel != null)
        {
            await _resourceOperationRegistry.Register(_typeId, Constants.Authorization.Operations.Delete);
        }

        var crudFeatures = CrudFeatures.None
            | (_getQueryable != null ? CrudFeatures.Read : 0)
            | (_createModel != null ? CrudFeatures.Create : 0)
            | (_updateModel != null ? CrudFeatures.Update : 0)
            | (_deleteModel != null ? CrudFeatures.Delete : 0);

        var modelHandler = new ModelHandler<T, TKey>
        {
            TypeId = _typeId,
            KeySelector = _keySelector ?? throw new ArgumentNullException(nameof(_keySelector)),
            GetQueryable = _getQueryable,
            CreateModel = _createModel,
            UpdateModel = _updateModel,
            DeleteModel = _deleteModel,
            CrudFeatures = crudFeatures,
            BuildInputModel = (propertyName) =>
            {
                if (_inputModelBuilders.TryGetValue(propertyName, out var builderFunc))
                {
                    return builderFunc.Invoke();
                }
                throw new ArgumentException($"No input model builder found for property '{propertyName}'.");
            },
        };

        if (_configureTableModel != null)
        {
            var tableModelBuilder = new TableModelBuilder<T, TKey>(_keySelector!, _tableViewPaths, modelHandler);
            _configureTableModel.Invoke(tableModelBuilder);
            modelHandler.BuildTableModel = tableModelBuilder.Build;
        }

        return modelHandler;
    }
}

