using System.Data;
using System.Linq.Expressions;
using Htmx.Components.Results;
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

    IActionResult RefreshAllViews(TableModel tableModel);
    IActionResult RefreshEditViews(TableModel tableModel);
}

public class TableProvider : ITableProvider
{
    private readonly TableViewPaths _paths;

    public TableProvider(TableViewPaths paths)
    {
        _paths = paths;
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

    public IActionResult RefreshAllViews(TableModel tableModel)
    {
        return new MultiSwapViewResult()
            .WithOobContent(_paths.Body, tableModel)
            .WithOobContent(_paths.Pagination, tableModel)
            .WithOobContent(_paths.Header, tableModel)
            .WithOobContent(_paths.HiddenValues, tableModel);
    }


    public IActionResult RefreshEditViews(TableModel tableModel)
    {
        var editRow = tableModel.Data.Where(r => r.IsEditing).SingleOrDefault();

        return new MultiSwapViewResult()
            .WithOobContent(_paths.HiddenValues, tableModel)
            .WithOobContent(_paths.Row, (tableModel, editRow));
    }
}