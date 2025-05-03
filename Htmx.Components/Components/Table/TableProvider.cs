using System.Data;
using System.Linq.Expressions;
using Htmx.Components.Extensions;
using Htmx.Components.Results;
using Htmx.Components.State;
using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.Table;

public interface ITableProvider
{
    TableModel<T, TKey> Build<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class;

    Task<TableModel<T, TKey>> BuildAndFetchPage<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        IQueryable<T> query,
        TableQueryParams queryParams,
        Action<TableModelBuilder<T, TKey>> config)
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
    private readonly IGlobalStateManager _globalStateManager;

    public TableProvider(TableViewPaths paths, IGlobalStateManager globalStateManager)
    {
        _paths = paths;
        _globalStateManager = globalStateManager;
    }

    public TableModel<T, TKey> Build<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class
    {
        var tableModelBuilder = new TableModelBuilder<T, TKey>(keySelector, _paths);
        config.Invoke(tableModelBuilder);
        var tableModel = tableModelBuilder.Build();
        tableModel.TableViewPaths = _paths;
        return tableModel;
    }

    public async Task<TableModel<T, TKey>> BuildAndFetchPage<T, TKey>(
        Expression<Func<T, TKey>> keySelector,
        IQueryable<T> query,
        TableQueryParams queryParams,
        Action<TableModelBuilder<T, TKey>> config)
        where T : class
    {
        var tableModelBuilder = new TableModelBuilder<T, TKey>(keySelector, _paths);
        config.Invoke(tableModelBuilder);
        var tableModel = await tableModelBuilder.BuildAndFetchPage(query, queryParams);
        tableModel.TableViewPaths = _paths;
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
}