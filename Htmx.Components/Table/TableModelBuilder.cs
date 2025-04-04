using System.Linq.Expressions;
using Htmx.Components.Table.Models;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Table;


/// <summary>
/// Abstracts the process of creating a <see cref="TableModel<typeparamref name="T"/>"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class TableModelBuilder<T> where T : class
{
    private readonly IQueryable<T> _queryable;
    private readonly TableQueryParams _queryParams;
    private readonly List<TableColumnModel<T>> _columns = new();

    public TableModelBuilder(IQueryable<T> query, TableQueryParams queryParams)
    {
        _queryable = query;
        _queryParams = queryParams;
    }

    /// <summary>
    /// Adds a TableColumnModel configured to be used as a value selector
    /// </summary>
    /// <param name="header"></param>
    /// <param name="selector"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public TableModelBuilder<T> AddSelectorColumn(string header, Expression<Func<T, object>> selector, Action<ColumnModelBuilder<T>>? configure = null)
    {
        var builder = new ColumnModelBuilder<T>(header);
        builder.Column.SelectorExpression = selector;
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
    public TableModelBuilder<T> AddHiddenColumn(string header, Expression<Func<T, object>> selector, Action<ColumnModelBuilder<T>>? configure = null)
    {
        var builder = new ColumnModelBuilder<T>(header);
        builder.Column.SelectorExpression = selector;
        builder.Column.Sortable = false;
        builder.Column.Filterable = false;
        builder.Column.IsHidden = true;
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
    public TableModelBuilder<T> AddDisplayColumn(string header, Action<ColumnModelBuilder<T>>? configure = null)
    {
        var builder = new ColumnModelBuilder<T>(header);
        builder.Column.Sortable = false;
        builder.Column.Filterable = false;
        configure?.Invoke(builder);
        _columns.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Uses the given columns and query params to extend the given queryable for appropriate
    /// filtering and sorting, and then executes the query twice; once with .CountAsync() so that
    /// PageCount can be calculated, and once with pagination applied.
    /// </summary>
    /// <returns>
    /// A fully configured <see cref="TableModel<typeparamref name="T"/>"/>  that's ready to be passed to a view
    /// </returns>
    public async Task<TableModel<T>> BuildAsync()
    {
        var query = _queryable;
        query = ApplyFiltering(query);
        query = ApplyRangeFiltering(query);
        query = ApplySorting(query);

        var totalCount = await query.CountAsync();
        var pageCount = (int)Math.Ceiling((double)totalCount / _queryParams.PageSize);
        var pagedData = await query
            .Skip((_queryParams.Page - 1) * _queryParams.PageSize)
            .Take(_queryParams.PageSize)
            .ToListAsync();

        return new TableModel<T>
        {
            Data = pagedData,
            Columns = _columns,
            PageCount = pageCount,
            Query = _queryParams
        };
    }

    private IQueryable<T> ApplyRangeFiltering(IQueryable<T> queryable)
    {
        // Find and pass any query param range filter values to their corresponding column range filter delegate
        // to apply it to the query
        if (_queryParams.RangeFilters != null)
        {
            foreach (var rangeFilter in _queryParams.RangeFilters.Where(f => !string.IsNullOrWhiteSpace(f.Value.Min)
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

    private IQueryable<T> ApplyFiltering(IQueryable<T> queryable)
    {
        // Find and pass any query param filter values to their corresponding column filter delegate
        // to apply it to the query
        if (_queryParams.Filters != null)
        {
            foreach (var filter in _queryParams.Filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
            {
                var column = _columns.FirstOrDefault(c => c.Header == filter.Key);
                if (column?.Filter != null)
                {
                    queryable = column.Filter(queryable, filter.Value);
                }
            }
        }

        return queryable;
    }

    private IQueryable<T> ApplySorting(IQueryable<T> queryable)
    {
        // In order for pagination to be consistent, we need to always define a sort.
        if (!string.IsNullOrEmpty(_queryParams.SortColumn))
        {
            var column = _columns.FirstOrDefault(c => c.Header == _queryParams.SortColumn);
            if (column != null)
            {
                queryable = _queryParams.SortDirection == "asc"
                    ? queryable.OrderBy(column.SelectorExpression)
                    : queryable.OrderByDescending(column.SelectorExpression);
            }
        }
        else
        {
            // Since no sort is specified, we'll just sort by the first selector column
            var column = _columns.FirstOrDefault(c => c.SelectorExpression != null);
            if (column == null)
                throw new InvalidOperationException("No selector column found for default sorting.");
            queryable = queryable.OrderBy(column.SelectorExpression);
        }

        return queryable;
    }
}

public class ColumnModelBuilder<T> where T : class
{
    internal readonly TableColumnModel<T> Column = new();

    public ColumnModelBuilder(string header)
    {
        Column.Header = header;
        // Default to Sortable and Filterable being true
        Column.Sortable = true;
        Column.Filterable = false;
    }

    public ColumnModelBuilder<T> WithCellPartial(string cellPartial)
    {
        Column.CellPartialView = cellPartial;
        return this;
    }

    public ColumnModelBuilder<T> WithFilterPartial(string filterPartial)
    {
        Column.FilterPartialView = filterPartial;
        return this;
    }

    public ColumnModelBuilder<T> WithFilter(Func<IQueryable<T>, string, IQueryable<T>> filter)
    {
        Column.Filter = filter;
        Column.Filterable = true;
        return this;
    }

    public ColumnModelBuilder<T> WithRangeFilter(Func<IQueryable<T>, string, string, IQueryable<T>> rangeFilter)
    {
        Column.RangeFilter = rangeFilter;
        return this;
    }

    public TableColumnModel<T> Build() => Column;
}


