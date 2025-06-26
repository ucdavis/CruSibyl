using Htmx.Components.Filters;
using Htmx.Components.Models;
using Htmx.Components.Table.Models;
using Htmx.Components.Table;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.Table.Internal;

/// <summary>
/// Internal filter that processes table refresh actions for out-of-band HTMX responses.
/// </summary>
/// <remarks>
/// This filter is automatically registered by the framework and processes actions
/// marked with <see cref="TableRefreshActionAttribute"/>.
/// </remarks>
internal class TableOobRefreshFilter : OobResultFilterBase<TableRefreshActionAttribute>
{
    private readonly ViewPaths _viewPaths;

    public TableOobRefreshFilter(ViewPaths viewPaths)
    {
        _viewPaths = viewPaths;
    }

    protected override Task UpdateMultiSwapViewResultAsync(TableRefreshActionAttribute attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        if (multiSwapViewResult.Model == null)
        {
            throw new InvalidOperationException($"MultiSwapViewResult must have a model set when filtering via {nameof(TableRefreshActionAttribute)}.");
        }
        var tableModel = (ITableModel)multiSwapViewResult.Model;
        multiSwapViewResult
            .WithOobContent(_viewPaths.Table.TableActionList, tableModel)
            .WithOobContent(_viewPaths.Table.EditClassToggle, tableModel)
            .WithOobContent(_viewPaths.Table.Body, tableModel)
            .WithOobContent(_viewPaths.Table.Pagination, tableModel)
            .WithOobContent(_viewPaths.Table.Header, tableModel);
        return Task.CompletedTask;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(TableRefreshActionAttribute attribute, ControllerActionDescriptor cad)
    {
        throw new InvalidOperationException($"{nameof(TableRefreshActionAttribute)} does not support non-HTMX requests.");
    }

}