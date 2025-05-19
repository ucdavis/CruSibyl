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
    private readonly TableViewPaths _paths;

    internal TableColumnModelBuilder(string header, TableViewPaths paths, ModelHandler<T, TKey> modelHandler, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _paths = paths;

        _model.Header = header;
        // Default to Sortable and Filterable being true
        _model.Sortable = true;
        _model.Filterable = false;
    }

    public TableColumnModelBuilder<T, TKey> WithEditable(bool isEditable = true)
    {
        _model.IsEditable = isEditable;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithCellPartial(string cellPartial)
    {
        _model.CellPartialView = cellPartial;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithFilterPartial(string filterPartial)
    {
        _model.FilterPartialView = filterPartial;
        _model.IsEditable = true;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithFilter(Func<IQueryable<T>, string, IQueryable<T>> filter)
    {
        _model.Filter = filter;
        _model.Filterable = true;
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithRangeFilter(Func<IQueryable<T>, string, string, IQueryable<T>> rangeFilter)
    {
        //TODO: not tested and probably won't work. need to figure out how to support different column types
        _model.RangeFilter = rangeFilter;
        _model.Filterable = true;
        if (string.IsNullOrWhiteSpace(_model.FilterPartialView))
        {
            _model.FilterPartialView = _paths.FilterDateRange;
        }
        return this;
    }

    public TableColumnModelBuilder<T, TKey> WithActions(Func<TableRowContext<T, TKey>, IEnumerable<ActionModel>> actionsFactory)
    {
        _model.ActionsFactory = actionsFactory;
        if (string.IsNullOrWhiteSpace(_model.CellPartialView))
        {
            _model.CellPartialView = _paths.CellActionList;
        }
        return this;
    }
}


