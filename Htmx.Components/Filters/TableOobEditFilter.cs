using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Table;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.Filters;

public class TableOobEditFilter : OobResultFilterBase<TableEditActionAttribute>
{
    private readonly ITableProvider _tableProvider;

    public TableOobEditFilter(ITableProvider tableProvider)
    {
        _tableProvider = tableProvider;
    }

    protected override Task UpdateMultiSwapViewResultAsync(TableEditActionAttribute attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        if (multiSwapViewResult.Model == null)
        {
            throw new InvalidOperationException($"MultiSwapViewResult must have a model set when filtering via {nameof(TableEditActionAttribute)}.");
        }
        var tableModel = (ITableModel)multiSwapViewResult.Model;
        multiSwapViewResult
            .WithOobContent(tableModel.TableViewPaths.EditClassToggle, tableModel)
            .WithOobContent(tableModel.TableViewPaths.TableActionList, tableModel);
        foreach (var row in tableModel.Rows)
        {
            multiSwapViewResult.WithOobContent(tableModel.TableViewPaths.Row, (tableModel, row),
                row.TargetDisposition ?? OobTargetDisposition.OuterHtml, row.TargetSelector);
        }
        return Task.CompletedTask;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(TableEditActionAttribute attribute, ControllerActionDescriptor cad)
    {
        throw new InvalidOperationException($"{nameof(TableEditActionAttribute)} does not support non-HTMX requests.");
    }
}