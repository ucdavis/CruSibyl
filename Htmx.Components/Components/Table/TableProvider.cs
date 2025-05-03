using System.Data;
using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Extensions;
using Htmx.Components.Results;
using Htmx.Components.State;
using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Table;

public interface ITableProvider
{
    /// <summary>
    /// Creates a new <see cref="TableModel{T, TKey}"/> with the given key selector and configuration.
    /// </summary>
    TableModel<T, TKey> Build<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class;

    /// <summary>
    /// Uses the given columns and tableState to extend the given queryable for appropriate
    /// filtering and sorting, and then executes the query twice; once with .CountAsync() so that
    /// PageCount can be calculated, and once with pagination applied. Places the results in the
    /// given <see cref="TableModel{T, TKey}"/>. The queryable is expected to be an EF Core queryable.
    /// </summary>
    /// <returns>
    /// A fully configured <see cref="TableModel{T, TKey}"/>  that's ready to be passed to a view
    /// </returns>
    Task<TableModel<T, TKey>> BuildAndFetchPage<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        IQueryable<T> query,
        TableState tableState,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class;

    /// <summary>
    /// Uses the given columns and tableState to extend the given queryable for appropriate
    /// filtering and sorting, and then executes the query twice; once with .CountAsync() so that
    /// PageCount can be calculated, and once with pagination applied. Places the results in the
    /// given <see cref="TableModel{T, TKey}"/>. The queryable is expected to be an EF Core queryable.
    /// </summary>
    Task FetchPage<T, TKey>(
        TableModel<T, TKey> tableModel,
        IQueryable<T> query,
        TableState tableState)
        where T : class;

    IActionResult RefreshAllViews(ITableModel tableModel);
    IActionResult RefreshEditViews(ITableModel tableModel);
}

public enum EditAction
{
    Create,
    Update,
    Delete,
}

public enum EditStatus
{
    Requested,
    Completed,
    Cancelled,
}

// Create Requested: send row_new with RowType.Editable
// Create Completed: Send row_new with RowType.Hidden and row_[new key] with RowType.ReadOnly
// Create Cancelled: Send row_new with RowType.Hidden
// Update Requested: Send row_[key] with RowType.Editable
// Update Completed: Send row_[key] with RowType.ReadOnly
// Update Cancelled: Send row_[key] with RowType.ReadOnly
// Delete Completed: Send row_[key] with RowType.Hidden


public class TableProvider : ITableProvider
{
    private readonly TableViewPaths _paths;
    private readonly IPageState _pageState;

    public TableProvider(TableViewPaths paths, IPageState pageState)
    {
        _paths = paths;
        _pageState = pageState;
    }

    /// <summary>
    /// Creates a new <see cref="TableModel{T, TKey}"/> with the given key selector and configuration.
    /// </summary>
    public TableModel<T, TKey> Build<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class
    {
        var tableModelBuilder = new TableModelBuilder<T, TKey>(keySelector, _paths);
        config.Invoke(tableModelBuilder);
        var tableModel = tableModelBuilder.Build();
        return tableModel;
    }

    /// <summary>
    /// Uses the given columns and tableState to extend the given queryable for appropriate
    /// filtering and sorting, and then executes the query twice; once with .CountAsync() so that
    /// PageCount can be calculated, and once with pagination applied. Places the results in the
    /// given <see cref="TableModel{T, TKey}"/>. The queryable is expected to be an EF Core queryable.
    /// </summary>
    public async Task FetchPage<T, TKey>(
        TableModel<T, TKey> tableModel,
        IQueryable<T> query,
        TableState tableState)
        where T : class
    {
        query = ApplyFiltering(query, tableState, tableModel);
        query = ApplyRangeFiltering(query, tableState, tableModel);
        query = ApplySorting(query, tableState, tableModel);

        var totalCount = await query.CountAsync();
        var pageCount = (int)Math.Ceiling((double)totalCount / tableState.PageSize);
        // make sure we're not trying to exceed the available pages
        tableState.Page = Math.Min(tableState.Page, pageCount);
        var pagedData = await query
            .Skip((tableState.Page - 1) * tableState.PageSize)
            .Take(tableState.PageSize)
            .ToListAsync();

        var keySelector = tableModel.KeySelector.CompileFast();

        tableModel.State = tableState;
        tableModel.PageCount = pageCount;
        tableModel.Rows = pagedData.Select((item, index) =>
        {
            var key = keySelector(item);
            var rowContext = new TableRowContext<T, TKey>
            {
                Item = item,
                PageIndex = tableState.Page - 1,
                Key = key
            };
            return rowContext;
        }).ToList();        
    }

    /// <summary>
    /// Uses the given columns and tableState to extend the given queryable for appropriate
    /// filtering and sorting, and then executes the query twice; once with .CountAsync() so that
    /// PageCount can be calculated, and once with pagination applied.
    /// </summary>
    /// <returns>
    /// A fully configured <see cref="TableModel<typeparamref name="T"/>"/>  that's ready to be passed to a view
    /// </returns>
    public async Task<TableModel<T, TKey>> BuildAndFetchPage<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        IQueryable<T> query,
        TableState tableState,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class
    {
        var tableModelBuilder = new TableModelBuilder<T, TKey>(keySelector, _paths);
        config.Invoke(tableModelBuilder);
        var tableModel = tableModelBuilder.Build();
        await FetchPage(tableModel, query, tableState);
        return tableModel;
    }

    public IActionResult RefreshAllViews(ITableModel tableModel)
    {
        if (tableModel.TableViewPaths == null)
        {
            tableModel.TableViewPaths = _paths;
        }
        
        return new MultiSwapViewResult()
            .WithOobContent(_paths.TableActionList, tableModel)
            .WithOobContent(_paths.EditClassToggle, tableModel)
            .WithOobContent(_paths.Body, tableModel)
            .WithOobContent(_paths.Pagination, tableModel)
            .WithOobContent(_paths.Header, tableModel);
    }


    public IActionResult RefreshEditViews(ITableModel tableModel)
    {
        if (tableModel.Rows.Count != 1)
        {
            throw new InvalidOperationException("RefreshEditViews requires exactly one row of data.");
        }

        if (tableModel.TableViewPaths == null)
        {
            tableModel.TableViewPaths = _paths;
        }

        return new MultiSwapViewResult()
            .WithOobContent(_paths.EditClassToggle, tableModel)
            .WithOobContent(_paths.TableActionList, tableModel)
            .WithOobContent(_paths.Row, (tableModel, tableModel.Rows[0]));
    }

    private IQueryable<T> ApplyRangeFiltering<T, TKey>(IQueryable<T> queryable, TableState tableState, TableModel<T, TKey> tableModel)
        where T : class
    {
        // Find and pass any query param range filter values to their corresponding column range filter delegate
        // to apply it to the query
        if (tableState.RangeFilters != null)
        {
            foreach (var rangeFilter in tableState.RangeFilters.Where(f => !string.IsNullOrWhiteSpace(f.Value.Min)
                    && !string.IsNullOrWhiteSpace(f.Value.Max)))
            {
                var column = tableModel.Columns.FirstOrDefault(c => c.Header == rangeFilter.Key);
                if (column?.RangeFilter != null)
                {
                    queryable = column.RangeFilter(queryable, rangeFilter.Value.Min, rangeFilter.Value.Max);
                }
            }
        }

        return queryable;
    }

    private IQueryable<T> ApplyFiltering<T, TKey>(IQueryable<T> query, TableState tableState, TableModel<T, TKey> tableModel)
        where T: class
    {
        // Find and pass any tableState filter values to their corresponding column filter delegate
        // to apply it to the query
        if (tableState.Filters != null)
        {
            foreach (var filter in tableState.Filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
            {
                var column = tableModel.Columns.FirstOrDefault(c => c.Header == filter.Key);
                if (column?.Filter != null)
                {
                    query = column.Filter(query, filter.Value);
                }
            }
        }

        return query;
    }

    private IQueryable<T> ApplySorting<T, TKey>(IQueryable<T> query, TableState tableState, TableModel<T, TKey> tableModel)
        where T : class
    {
        // In order for pagination to be consistent, we need to always define a sort.
        if (!string.IsNullOrEmpty(tableState.SortColumn))
        {
            var column = tableModel.Columns.FirstOrDefault(c => c.Header == tableState.SortColumn);
            if (column != null)
            {
                query = tableState.SortDirection == "asc"
                    ? query.OrderBy(column.SelectorExpression)
                    : query.OrderByDescending(column.SelectorExpression);
            }
        }
        else
        {
            // Since no sort is specified, we'll just sort by the first selector column
            var column = tableModel.Columns.FirstOrDefault(c => c.SelectorExpression != null);
            if (column == null)
                throw new InvalidOperationException("No selector column found for default sorting.");
            query = query.OrderBy(column.SelectorExpression);
        }

        return query;
    }

}