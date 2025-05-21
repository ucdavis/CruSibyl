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
        where T : class, new();
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
        where T : class, new()
    {
        var builder = new ModelHandlerBuilder<T, TKey>(_serviceProvider, typeId, _tableViewPaths, _resourceOperationRegistry);
        config.Invoke(_serviceProvider, builder);
        var castTask = async () =>
        {
            var modelHandler = await builder.Build();
            return (ModelHandler)modelHandler;
        };
        _modelHandlers[typeId] = castTask();
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


