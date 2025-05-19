using System.Linq.Expressions;
using Htmx.Components.Authorization;
using Htmx.Components.Extensions;
using Htmx.Components.Models.Table;

namespace Htmx.Components.Models.Builders;

public class ModelHandlerBuilder<T, TKey> : BuilderBase<ModelHandlerBuilder<T, TKey>, ModelHandler<T, TKey>>
    where T : class, new()
{
    internal ModelHandlerBuilder(IServiceProvider serviceProvider, string typeId, TableViewPaths tableViewPaths, IResourceOperationRegistry resourceOperationRegistry)
        : base(serviceProvider)
    {
        _resourceOperationRegistry = resourceOperationRegistry;
        _model.TypeId = typeId;
        _model.Paths = tableViewPaths;
        _model.ServiceProvider = serviceProvider;
    }

    private readonly IResourceOperationRegistry _resourceOperationRegistry;

    public ModelHandlerBuilder<T, TKey> WithTypeId(string typeId)
    {
        _model.TypeId = typeId;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithKeySelector(Expression<Func<T, TKey>> keySelector)
    {
        _model.KeySelector = keySelector;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithQueryable(Func<IQueryable<T>> getQueryable)
    {
        _model.GetQueryable = getQueryable;
        _model.CrudFeatures |= CrudFeatures.Read;
        AddBuildTask(_resourceOperationRegistry.Register(_model.TypeId, Constants.Authorization.Operations.Read));
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithCreateModel(Func<T, Task<Result>> createModel)
    {
        _model.CreateModel = createModel;
        _model.CrudFeatures |= CrudFeatures.Create;
        AddBuildTask(_resourceOperationRegistry.Register(_model.TypeId, Constants.Authorization.Operations.Create));
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithUpdateModel(Func<T, Task<Result>> updateModel)
    {
        _model.UpdateModel = updateModel;
        _model.CrudFeatures |= CrudFeatures.Update;
        AddBuildTask(_resourceOperationRegistry.Register(_model.TypeId, Constants.Authorization.Operations.Update));
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithDeleteModel(Func<TKey, Task<Result>> deleteModel)
    {
        _model.DeleteModel = deleteModel;
        _model.CrudFeatures |= CrudFeatures.Delete;
        AddBuildTask(_resourceOperationRegistry.Register(_model.TypeId, Constants.Authorization.Operations.Delete));
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithTableModel(Action<TableModelBuilder<T, TKey>> configure)
    {
        _model.ConfigureTableModel = configure;
        return this;
    }

    public ModelHandlerBuilder<T, TKey> WithInputModel<TProp>(Expression<Func<T, TProp>> propertySelector,
        Action<InputModelBuilder<T, TProp>> configure)
    {
        var propName = propertySelector.GetPropertyName();
        var builder = new InputModelBuilder<T, TProp>(_serviceProvider, propertySelector);
        AddBuildTask(async () =>
        {
            var inputModel = await builder.Build();
            _model.InputModelBuilders ??= new Dictionary<string, Func<IInputModel>>();
            _model.InputModelBuilders[propName] = () => inputModel;
        });
        return this;
    }
    
    public async Task<ModelHandler> BuildModelHandler()
    {
        var modelHandler = await Build();
        return modelHandler;
    }
}
