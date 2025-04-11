using Htmx.Components.Results;
using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.Table;

public interface ITableProvider
{
    Task<TableModel<T>> BuildAsync<T>(
            IQueryable<T> query,
            TableQueryParams queryParams,
            Action<TableModelBuilder<T>> config)
            where T : class;
    IActionResult RefreshView(TableModel tableModel);
}

public class TableProvider : ITableProvider
{
    private readonly TableViewPaths _paths;

    public TableProvider(TableViewPaths paths)
    {
        _paths = paths;
    }

    public async Task<TableModel<T>> BuildAsync<T>(
        IQueryable<T> query,
        TableQueryParams queryParams,
        Action<TableModelBuilder<T>> config)
        where T : class
    {
        var tableModelBuilder = new TableModelBuilder<T>(_paths);
        config.Invoke(tableModelBuilder);
        var tableModel = await tableModelBuilder.BuildAsync(query, queryParams);
        return tableModel;
    }

    public IActionResult RefreshView(TableModel tableModel)
    {
        return new MultiSwapViewResult()
            .WithOobContent(_paths.Body, tableModel)
            .WithOobContent(_paths.Pagination, tableModel)
            .WithOobContent(_paths.Header, tableModel);
    }
}