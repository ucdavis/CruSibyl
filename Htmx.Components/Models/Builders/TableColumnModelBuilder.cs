using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Services;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Models.Builders;


public class TableColumnModelBuilder<T, TKey> : BuilderBase<TableColumnModelBuilder<T, TKey>, TableColumnModel<T, TKey>>
    where T : class
{
    private readonly TableColumnModelConfig<T, TKey> _config;

    internal TableColumnModelBuilder(TableColumnModelConfig<T, TKey> config, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _config = config;
    }

    public TableColumnModelBuilder<T, TKey> WithEditable(bool isEditable = true)
    {
        if (!(_config.ModelHandler?.InputModelBuilders?.TryGetValue(_config.DataName, out var inputModelBuilder) == true))
        {
            throw new InvalidOperationException($"No input model builder found for column '{_config.DataName}'. Ensure that the input model is registered in the ModelHandler.");
        }
        _config.IsEditable = isEditable;
        if (isEditable)
        {
            _config.GetInputModel = async (rowContext) =>
            {
                var inputModel = await inputModelBuilder.Invoke(rowContext.ModelHandler);
                inputModel.ObjectValue = _config.SelectorFunc!(rowContext.Item);
                return inputModel;
            };
        }
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithCellPartial(string cellPartial)
    {
        _config.CellPartialView = cellPartial;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithFilterPartial(string filterPartial)
    {
        _config.FilterPartialView = filterPartial;
        _config.IsEditable = true;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithFilter(Func<IQueryable<T>, string, IQueryable<T>> filter)
    {
        _config.Filter = filter;
        _config.Filterable = true;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithRangeFilter(Func<IQueryable<T>, string, string, IQueryable<T>> rangeFilter)
    {
        //TODO: not tested and probably won't work. need to figure out how to support different column types
        _config.RangeFilter = rangeFilter;
        _config.Filterable = true;
        if (string.IsNullOrWhiteSpace(_config.FilterPartialView))
        {
            _config.FilterPartialView = _config.Paths.FilterDateRange;
        }
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithActions(Action<TableRowContext<T, TKey>, ActionSetBuilder> actionsFactory)
    {
        _config.ActionsFactory = async (rowContext) =>
        {
            var actionSetBuilder = new ActionSetBuilder(_serviceProvider);
            actionsFactory.Invoke(rowContext, actionSetBuilder);
            var actionSet = await actionSetBuilder.Build();
            return actionSet.Items.Cast<ActionModel>();
        };
        if (string.IsNullOrWhiteSpace(_config.CellPartialView))
        {
            _config.CellPartialView = _config.Paths.CellActionList;
        }
        return this;
    }

    protected override Task<TableColumnModel<T, TKey>> BuildImpl()
    {
        if (_config.SelectorFunc == null && _config.SelectorExpression != null)
        {
            _config.SelectorFunc = _config.SelectorExpression.CompileFast();
        }

        var model = new TableColumnModel<T, TKey>(_config);
        return Task.FromResult(model);
    }
}


