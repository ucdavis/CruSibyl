using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Table;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.Filters;

public class TableOobConverterFilter : IAsyncResultFilter
{
    private readonly ITableProvider _tableProvider;

    public TableOobConverterFilter(ITableProvider tableProvider)
    {
        _tableProvider = tableProvider;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Check for marker attributes
        var hasTableEditActionAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<TableEditActionAttribute>().Any();
        var hasTableRefreshActionAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<TableRefreshActionAttribute>().Any();

        // Make sure we have a TableModel in the result
        if ((hasTableEditActionAttribute || hasTableRefreshActionAttribute)
            && context.Result is ObjectResult objResult && objResult.Value is ITableModel tableModel)
        {
            // Replace the result with a MultiSwapViewResult
            if (hasTableEditActionAttribute)
            {
                var result = new MultiSwapViewResult()
                    .WithOobContent(tableModel.TableViewPaths.EditClassToggle, tableModel)
                    .WithOobContent(tableModel.TableViewPaths.TableActionList, tableModel);
                foreach (var row in tableModel.Rows)
                {
                    result.WithOobContent(tableModel.TableViewPaths.Row, (tableModel, row),
                        row.TargetDisposition ?? OobTargetDisposition.OuterHtml, row.TargetSelector);
                }
                context.Result = result;
            }
            else if (hasTableRefreshActionAttribute)
            {
                context.Result = new MultiSwapViewResult()
                    .WithOobContent(tableModel.TableViewPaths.TableActionList, tableModel)
                    .WithOobContent(tableModel.TableViewPaths.EditClassToggle, tableModel)
                    .WithOobContent(tableModel.TableViewPaths.Body, tableModel)
                    .WithOobContent(tableModel.TableViewPaths.Pagination, tableModel)
                    .WithOobContent(tableModel.TableViewPaths.Header, tableModel);
            }
        }

        await next();
    }
}