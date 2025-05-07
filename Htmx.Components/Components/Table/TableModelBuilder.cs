using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Action;
using Htmx.Components.Extensions;
using Htmx.Components.Table.Models;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Table;


/// <summary>
/// Abstracts the process of creating a <see cref="TableModel<typeparamref name="T"/>"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class TableModelBuilder<T, TKey> where T : class
{
    private readonly List<TableColumnModel<T, TKey>> _columns = new();
    private readonly TableViewPaths _paths;
    private Expression<Func<T, TKey>> _keySelector;
    private Func<TableModel<T, TKey>, IEnumerable<ActionModel>> _actionsFactory = _ => [];
    private string _typeId = typeof(T).Name;

    internal TableModelBuilder(Expression<Func<T, TKey>> keySelector, TableViewPaths paths)
    {
        _keySelector = keySelector;
        _paths = paths;
    }


    /// <summary>
    /// Adds a TableColumnModel configured to be used as a value selector
    /// </summary>
    /// <param name="header"></param>
    /// <param name="selector"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T, TKey> AddSelectorColumn(string header, Expression<Func<T, object>> selector, 
        Action<ColumnModelBuilder<T, TKey>>? configure = null)
    {
        var builder = new ColumnModelBuilder<T, TKey>(header, _paths);
        builder.Column.SelectorExpression = selector;
        builder.Column.DataType = selector.GetMemberType();
        builder.Column.DataName = selector.GetPropertyName();
        builder.Column.ColumnType = ColumnType.ValueSelector;
        configure?.Invoke(builder);
        _columns.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a TableColumnModel configured to be used as a display column
    /// </summary>
    /// <param name="header"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T, TKey> AddDisplayColumn(string header, Action<ColumnModelBuilder<T, TKey>>? configure = null)
    {
        var builder = new ColumnModelBuilder<T, TKey>(header, _paths);
        builder.Column.Sortable = false;
        builder.Column.Filterable = false;
        builder.Column.ColumnType = ColumnType.Display;
        configure?.Invoke(builder);
        _columns.Add(builder.Build());
        return this;
    }


    public TableModelBuilder<T, TKey> WithActions(Func<TableModel<T, TKey>, IEnumerable<ActionModel>> actionsFactory)
    {
        _actionsFactory = actionsFactory;
        return this;
    }

    public TableModelBuilder<T, TKey> WithTypeId(string typeId)
    {
        _typeId = typeId;
        return this;
    }

    /// <summary>
    /// Returns a TableModel with the configured columns
    /// </summary>
    public TableModel<T, TKey> Build()
    {
        var tableModel = new TableModel<T, TKey>
        {
            TypeId = _typeId,
            Columns = _columns,
            ActionsFactory = _actionsFactory,
            TableViewPaths = _paths,
            KeySelector = _keySelector,
        };

        foreach (var column in _columns)
        {
            column.Table = tableModel;
        }

        return tableModel;
    }
}

public class ColumnModelBuilder<T, TKey> where T : class
{
    internal readonly TableColumnModel<T, TKey> Column = new();
    private readonly TableViewPaths _paths;

    internal ColumnModelBuilder(string header, TableViewPaths paths)
    {
        _paths = paths;

        Column.Header = header;
        // Default to Sortable and Filterable being true
        Column.Sortable = true;
        Column.Filterable = false;
    }

    public ColumnModelBuilder<T, TKey> WithEditable(bool isEditable = true)
    {
        Column.IsEditable = isEditable;
        return this;
    }

    public ColumnModelBuilder<T, TKey> WithCellPartial(string cellPartial)
    {
        Column.CellPartialView = cellPartial;
        return this;
    }

    public ColumnModelBuilder<T, TKey> WithFilterPartial(string filterPartial)
    {
        Column.FilterPartialView = filterPartial;
        Column.IsEditable = true;
        return this;
    }

    public ColumnModelBuilder<T, TKey> WithFilter(Func<IQueryable<T>, string, IQueryable<T>> filter)
    {
        Column.Filter = filter;
        Column.Filterable = true;
        return this;
    }

    public ColumnModelBuilder<T, TKey> WithRangeFilter(Func<IQueryable<T>, string, string, IQueryable<T>> rangeFilter)
    {
        //TODO: not tested and probably won't work. need to figure out how to support different column types
        Column.RangeFilter = rangeFilter;
        Column.Filterable = true;
        if (string.IsNullOrWhiteSpace(Column.FilterPartialView))
        {
            Column.FilterPartialView = _paths.FilterDateRange;
        }
        return this;
    }

    public ColumnModelBuilder<T, TKey> WithActions(Func<TableRowContext<T, TKey>, IEnumerable<ActionModel>> actionsFactory)
    {
        Column.ActionsFactory = actionsFactory;
        if (string.IsNullOrWhiteSpace(Column.CellPartialView))
        {
            Column.CellPartialView = _paths.CellActionList;
        }
        return this;
    }

    public TableColumnModel<T, TKey> Build() => Column;
}


