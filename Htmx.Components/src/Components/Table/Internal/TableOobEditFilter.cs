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
/// Internal filter that processes table edit actions for out-of-band HTMX responses.
/// </summary>
/// <remarks>
/// This filter is automatically registered by the framework and processes actions
/// marked with <see cref="TableEditActionAttribute"/>.
/// </remarks>
internal class TableOobEditFilter : OobResultFilterBase<TableEditActionAttribute>
{
    private readonly ITableProvider _tableProvider;
    private readonly ViewPaths _viewPaths;

    public TableOobEditFilter(ITableProvider tableProvider, ViewPaths viewPaths)
    {
        _tableProvider = tableProvider;
        _viewPaths = viewPaths;
    }

    protected override Task UpdateMultiSwapViewResultAsync(TableEditActionAttribute attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        if (multiSwapViewResult.Model == null)
        {
            throw new InvalidOperationException($"MultiSwapViewResult must have a model set when filtering via {nameof(TableEditActionAttribute)}.");
        }
        var tableModel = (ITableModel)multiSwapViewResult.Model;
        multiSwapViewResult
            .WithOobContent(_viewPaths.Table.EditClassToggle, tableModel)
            .WithOobContent(_viewPaths.Table.TableActionList, tableModel);
        foreach (var row in tableModel.Rows)
        {
            multiSwapViewResult.WithOobContent(_viewPaths.Table.Row, (tableModel, row),
                row.TargetDisposition ?? OobTargetDisposition.OuterHtml, row.TargetSelector);
        }
        return Task.CompletedTask;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(TableEditActionAttribute attribute, ControllerActionDescriptor cad)
    {
        throw new InvalidOperationException($"{nameof(TableEditActionAttribute)} does not support non-HTMX requests.");
    }
}