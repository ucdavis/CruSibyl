using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Services;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Models.Builders;


/// <summary>
/// Abstracts the process of creating a <see cref="TableModel<typeparamref name="T"/>"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class TableModelBuilder<T, TKey> : BuilderBase<TableModelBuilder<T, TKey>, TableModel<T, TKey>>
    where T : class
{
    private readonly TableModelConfig<T, TKey> _config = new();

    internal TableModelBuilder(Expression<Func<T, TKey>> keySelector, TableViewPaths paths, ModelHandler<T, TKey> modelHandler, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _config.KeySelector = keySelector;
        _config.ModelHandler = modelHandler;
        _config.TypeId = modelHandler.TypeId;
        _config.TableViewPaths = paths;
    }


    /// <summary>
    /// Adds a TableColumnModel configured to be used as a value selector
    /// </summary>
    /// <param name="header"></param>
    /// <param name="selector"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T, TKey> AddSelectorColumn(Expression<Func<T, object>> selector,
        Action<TableColumnModelBuilder<T, TKey>>? configure = null)
    {
        AddBuildTask(async () =>
        {
            var propertyName = selector.GetPropertyName();
            var header = propertyName.Humanize();
            var config = new TableColumnModelConfig<T, TKey>
            {
                Display = new TableColumnDisplayOptions
                {
                    Header = header,
                    DataName = propertyName,
                    ColumnType = ColumnType.ValueSelector
                },
                DataOptions = new TableColumnDataOptions<T, TKey>
                {
                    SelectorExpression = selector,
                    Paths = _config.TableViewPaths!,
                    ModelHandler = _config.ModelHandler!
                }
            };
            var builder = new TableColumnModelBuilder<T, TKey>(config, _serviceProvider);
            configure?.Invoke(builder);
            var columnModel = await builder.Build();
            _config.Columns.Add(columnModel);
        });
        return this;
    }

    /// <summary>
    /// Adds a TableColumnModel configured to be used as a display column
    /// </summary>
    /// <param name="header"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T, TKey> AddDisplayColumn(string header, Action<TableColumnModelBuilder<T, TKey>>? configure = null)
    {
        AddBuildTask(async () =>
        {
            var config = new TableColumnModelConfig<T, TKey>
            {
                Display = new TableColumnDisplayOptions
                {
                    Header = header,
                    ColumnType = ColumnType.Display
                },
                Behavior = new TableColumnBehaviorOptions
                {
                    Sortable = false,
                    Filterable = false
                },
                DataOptions = new TableColumnDataOptions<T, TKey>
                {
                    Paths = _config.TableViewPaths!,
                    ModelHandler = _config.ModelHandler!
                }
            };
            var builder = new TableColumnModelBuilder<T, TKey>(config, _serviceProvider);
            configure?.Invoke(builder);
            var columnModel = await builder.Build();
            _config.Columns.Add(columnModel);
        });
        return this;
    }

    public TableModelBuilder<T, TKey> AddCrudDisplayColumn(string header = "Actions")
    {
        return AddDisplayColumn("Actions", col => col.WithCrudActions());
    }


    public TableModelBuilder<T, TKey> WithActions(Action<TableModel<T, TKey>, ActionSetBuilder> actionsFactory)
    {
        AddBuildTask(() =>
        {
            _config.ActionsFactories.Add(async (tableModel) =>
            {
                var actionSetBuilder = new ActionSetBuilder(_serviceProvider);
                actionsFactory.Invoke(tableModel, actionSetBuilder);
                var actionSet = await actionSetBuilder.Build();
                return actionSet.Items.Cast<ActionModel>();
            });
        });
        return this;
    }

    public TableModelBuilder<T, TKey> WithCrudActions()
    {
        var typeId = _config.TypeId!;
        return WithActions((table, actions) =>
            actions.AddAction(action => action
                .WithLabel("Add New")
                .WithIcon("fas fa-plus mr-1")
                .WithHxPost($"/Form/{typeId}/Table/Create")
        ));
    }

    public TableModelBuilder<T, TKey> WithTypeId(string typeId)
    {
        _config.TypeId = typeId;
        return this;
    }

    protected override Task<TableModel<T, TKey>> BuildImpl()
    {
        var model = new TableModel<T, TKey>(_config);
        foreach (var column in model.Columns)
        {
            column.Table = model;
        }
        return Task.FromResult(model);
    }
}
