using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Services;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Models.Builders;


/// <summary>
/// Abstracts the process of creating a <see cref="TableModel<typeparamref name="T"/>"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class TableModelBuilder<T, TKey> : BuilderBase<TableModelBuilder<T, TKey>, TableModel<T, TKey>>
    where T : class
{
    internal TableModelBuilder(Expression<Func<T, TKey>> keySelector, TableViewPaths paths, ModelHandler<T, TKey> modelHandler, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _model.KeySelector = keySelector;
        _model.ModelHandler = modelHandler;
        _model.TypeId = modelHandler.TypeId;
        _model.TableViewPaths = paths;
    }


    /// <summary>
    /// Adds a TableColumnModel configured to be used as a value selector
    /// </summary>
    /// <param name="header"></param>
    /// <param name="selector"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T, TKey> AddSelectorColumn(string header, Expression<Func<T, object>> selector,
        Action<TableColumnModelBuilder<T, TKey>>? configure = null)
    {
        AddBuildTask(BuildPhase.Columns, async () =>
        {
            var builder = new TableColumnModelBuilder<T, TKey>(header, _model.TableViewPaths, _model.ModelHandler, _serviceProvider);
            builder.IncompleteModel.SelectorExpression = selector;
            builder.IncompleteModel.DataName = selector.GetPropertyName();
            builder.IncompleteModel.ColumnType = ColumnType.ValueSelector;
            configure?.Invoke(builder);
            var columnModel = await builder.Build();
            _model.Columns.Add(columnModel);
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
        AddBuildTask(BuildPhase.Columns, async () =>
        {
            var builder = new TableColumnModelBuilder<T, TKey>(header, _model.TableViewPaths, _model.ModelHandler, _serviceProvider);
            builder.IncompleteModel.Sortable = false;
            builder.IncompleteModel.Filterable = false;
            builder.IncompleteModel.ColumnType = ColumnType.Display;
            configure?.Invoke(builder);
            var columnModel = await builder.Build();
            _model.Columns.Add(columnModel);
        });
        return this;
    }


    public TableModelBuilder<T, TKey> WithActions(Action<TableModel<T, TKey>, ActionSetBuilder> actionsFactory)
    {
        AddBuildTask(BuildPhase.Actions, async () =>
        {
            var actionSetBuilder = new ActionSetBuilder(_serviceProvider);
            actionsFactory.Invoke(_model, actionSetBuilder);
            var actionSet = await actionSetBuilder.Build();
            _model.ActionsFactory = () => actionSet.Items.Cast<ActionModel>();
        });
        return this;
    }

    public TableModelBuilder<T, TKey> WithTypeId(string typeId)
    {
        _model.TypeId = typeId;
        return this;
    }

    internal override async Task<TableModel<T, TKey>> Build()
    {
        var model = await base.Build();
        foreach (var column in model.Columns)
        {
            column.Table = model;
        }
        return model;
    }
}
