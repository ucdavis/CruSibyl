using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Table;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.Filters;

public class TableOobRefreshFilter : OobResultFilterBase<TableRefreshActionAttribute>
{
    protected override Task UpdateMultiSwapViewResultAsync(TableRefreshActionAttribute attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        if (multiSwapViewResult.Model == null)
        {
            throw new InvalidOperationException($"MultiSwapViewResult must have a model set when filtering via {nameof(TableRefreshActionAttribute)}.");
        }
        var tableModel = (ITableModel)multiSwapViewResult.Model;
        multiSwapViewResult
            .WithOobContent(tableModel.TableViewPaths.TableActionList, tableModel)
            .WithOobContent(tableModel.TableViewPaths.EditClassToggle, tableModel)
            .WithOobContent(tableModel.TableViewPaths.Body, tableModel)
            .WithOobContent(tableModel.TableViewPaths.Pagination, tableModel)
            .WithOobContent(tableModel.TableViewPaths.Header, tableModel);
        return Task.CompletedTask;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(TableRefreshActionAttribute attribute, ControllerActionDescriptor cad)
    {
        throw new InvalidOperationException($"{nameof(TableRefreshActionAttribute)} does not support non-HTMX requests.");
    }

}