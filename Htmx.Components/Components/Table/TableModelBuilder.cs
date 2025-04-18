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
        builder.Column.ColumnType = ColumnType.ValueSelector;
        configure?.Invoke(builder);
        _columns.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a TableColumnModel configured to be used as a hidden column
    /// </summary>
    /// <param name="header"></param>
    /// <param name="selector"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T, TKey> AddHiddenColumn(string header, Expression<Func<T, object>> selector, 
        Action<ColumnModelBuilder<T, TKey>>? configure = null)
    {
        var builder = new ColumnModelBuilder<T, TKey>(header, _paths);
        builder.Column.SelectorExpression = selector;
        builder.Column.Sortable = false;
        builder.Column.Filterable = false;
        builder.Column.ColumnType = ColumnType.Hidden;
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

    /// <summary>
    /// Returns a TableModel with the configured columns
    /// </summary>
    public TableModel<T, TKey> Build()
    {
        return new TableModel<T, TKey>
        {
            Columns = _columns,
            ActionsFactory = _actionsFactory
        };        
    }

    /// <summary>
    /// Uses the given columns and query params to extend the given queryable for appropriate
    /// filtering and sorting, and then executes the query twice; once with .CountAsync() so that
    /// PageCount can be calculated, and once with pagination applied.
    /// </summary>
    /// <returns>
    /// A fully configured <see cref="TableModel<typeparamref name="T"/>"/>  that's ready to be passed to a view
    /// </returns>
    internal async Task<TableModel<T, TKey>> BuildAndFetchPage(IQueryable<T> query, TableQueryParams queryParams)
    {
        query = ApplyFiltering(query, queryParams);
        query = ApplyRangeFiltering(query, queryParams);
        query = ApplySorting(query, queryParams);

        var totalCount = await query.CountAsync();
        var pageCount = (int)Math.Ceiling((double)totalCount / queryParams.PageSize);
        // make sure we're not trying to exceed the available pages
        queryParams.Page = Math.Min(queryParams.Page, pageCount);
        var pagedData = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        var keySelector = _keySelector.CompileFast();

        return new TableModel<T, TKey>
        {
            Rows = pagedData.Select((item, index) =>
            {
                var key = keySelector(item);
                var rowContext = new TableRowContext<T, TKey>
                {
                    Item = item,
                    PageIndex = 0,
                    Key = key
                };
                return rowContext;
            }).ToList(),
            Columns = _columns,
            ActionsFactory = _actionsFactory,
            PageCount = pageCount,
            Query = queryParams
        };
    }

    private IQueryable<T> ApplyRangeFiltering(IQueryable<T> queryable, TableQueryParams queryParams)
    {
        // Find and pass any query param range filter values to their corresponding column range filter delegate
        // to apply it to the query
        if (queryParams.RangeFilters != null)
        {
            foreach (var rangeFilter in queryParams.RangeFilters.Where(f => !string.IsNullOrWhiteSpace(f.Value.Min)
                    && !string.IsNullOrWhiteSpace(f.Value.Max)))
            {
                var column = _columns.FirstOrDefault(c => c.Header == rangeFilter.Key);
                if (column?.RangeFilter != null)
                {
                    queryable = column.RangeFilter(queryable, rangeFilter.Value.Min, rangeFilter.Value.Max);
                }
            }
        }

        return queryable;
    }

    private IQueryable<T> ApplyFiltering(IQueryable<T> query, TableQueryParams queryParams)
    {
        // Find and pass any query param filter values to their corresponding column filter delegate
        // to apply it to the query
        if (queryParams.Filters != null)
        {
            foreach (var filter in queryParams.Filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
            {
                var column = _columns.FirstOrDefault(c => c.Header == filter.Key);
                if (column?.Filter != null)
                {
                    query = column.Filter(query, filter.Value);
                }
            }
        }

        return query;
    }

    private IQueryable<T> ApplySorting(IQueryable<T> query, TableQueryParams queryParams)
    {
        // In order for pagination to be consistent, we need to always define a sort.
        if (!string.IsNullOrEmpty(queryParams.SortColumn))
        {
            var column = _columns.FirstOrDefault(c => c.Header == queryParams.SortColumn);
            if (column != null)
            {
                query = queryParams.SortDirection == "asc"
                    ? query.OrderBy(column.SelectorExpression)
                    : query.OrderByDescending(column.SelectorExpression);
            }
        }
        else
        {
            // Since no sort is specified, we'll just sort by the first selector column
            var column = _columns.FirstOrDefault(c => c.SelectorExpression != null);
            if (column == null)
                throw new InvalidOperationException("No selector column found for default sorting.");
            query = query.OrderBy(column.SelectorExpression);
        }

        return query;
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

    public ColumnModelBuilder<T, TKey> WithCellPartial(string cellPartial)
    {
        Column.CellPartialView = cellPartial;
        return this;
    }

    public ColumnModelBuilder<T, TKey> WithFilterPartial(string filterPartial)
    {
        Column.FilterPartialView = filterPartial;
        Column.Editable = true;
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


