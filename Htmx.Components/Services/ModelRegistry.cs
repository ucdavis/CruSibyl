using System.Linq.Expressions;
using Htmx.Components.Authorization;
using Htmx.Components.Extensions;
using Htmx.Components.Input;
using Htmx.Components.Models;
using Htmx.Components.Models.Builders;
using Htmx.Components.Models.Table;
using Htmx.Components.Table;
using Microsoft.Extensions.DependencyInjection;

namespace Htmx.Components.Services;


public interface IModelRegistry
{
    void Register<T, TKey>(string typeId, Action<IServiceProvider, ModelHandlerBuilder<T, TKey>> config)
        where T : class, new();
    Task<ModelHandler> GetModelHandler(string typeId, ModelUI modelUI);
    Task<ModelHandler<T, TKey>> GetModelHandler<T, TKey>(string typeId, ModelUI modelUI)
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
        where T : class, new()
    {
        var builder = new ModelHandlerBuilder<T, TKey>(_serviceProvider, typeId, _tableViewPaths, _resourceOperationRegistry);
        config.Invoke(_serviceProvider, builder);
        var castTask = async () =>
        {
            var modelHandler = await builder.BuildAsync();
            return (ModelHandler)modelHandler;
        };
        _modelHandlers[typeId] = castTask();
    }

    public async Task<ModelHandler> GetModelHandler(string typeId, ModelUI modelUI)
    {
        var task = _modelHandlers.TryGetValue(typeId, out var handler) ? handler : null;
        if (task == null)
        {
            throw new InvalidOperationException($"Model handler for typeId '{typeId}' not found.");
        }
        var modelHandler = await task;
        modelHandler.ModelUI = modelUI;
        return modelHandler;
    }

    public async Task<ModelHandler<T, TKey>> GetModelHandler<T, TKey>(string typeId, ModelUI modelUI)
        where T : class
    {
        var handler = await GetModelHandler(typeId, modelUI);
        if (handler is ModelHandler<T, TKey> typedHandler)
        {
            return typedHandler;
        }
        throw new InvalidOperationException($"Model handler for typeId '{typeId}' is not of type '{typeof(ModelHandler<T, TKey>)}'.");
    }
}

/// <summary>
/// Factory interface for creating model handlers.
/// Inject this into your controllers rather than <seealso cref="IModelRegistry"/>"/> in 
/// order to avoid DI recursion issues.
/// </summary>
public interface IModelHandlerFactory
{
    Task<ModelHandler> Get(string typeId, ModelUI modelUI);
}

/// <summary>
/// Factory interface for creating model handlers.
/// Inject this into your controllers rather than <seealso cref="IModelRegistry"/>"/> in 
/// order to avoid DI recursion issues.
/// </summary>
public interface IModelHandlerFactoryGeneric
{
    Task<ModelHandler<T, TKey>> Get<T, TKey>(string typeId, ModelUI modelUI)
        where T : class;
}

public class ModelHandlerFactory : IModelHandlerFactory, IModelHandlerFactoryGeneric
{
    private readonly IServiceProvider _serviceProvider;

    public ModelHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<ModelHandler> Get(string typeId, ModelUI modelUI)
    {
        var registry = _serviceProvider.GetRequiredService<IModelRegistry>();
        return registry.GetModelHandler(typeId, modelUI);
    }

    public Task<ModelHandler<T, TKey>> Get<T, TKey>(string typeId, ModelUI modelUI)
        where T : class
    {
        var registry = _serviceProvider.GetRequiredService<IModelRegistry>();
        return registry.GetModelHandler<T, TKey>(typeId, modelUI);
    }
}