using Htmx.Components.Attributes;
using Htmx.Components.NavBar;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Htmx.Components.Filters;

/// <summary>
/// Base class for OOB result filters that handle specific attributes. Does most of the work,
/// so that derived classes only need to implement the view name retrieval and multi-swap update logic. 
/// </summary>
/// <typeparam name="T">The</typeparam>
public abstract class OobResultFilterBase<T> : IAsyncResultFilter
    where T : Attribute
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor cad)
        {
            var attribute = cad.MethodInfo.GetCustomAttributes(typeof(T), true).Cast<T>().FirstOrDefault();
            if (attribute != null && (context.Result is ObjectResult || context.Result is MultiSwapViewResult))
            {
                if (context.HttpContext.Request.IsHtmx())
                {
                    MultiSwapViewResult multiSwapViewResult = null!;
                    if (context.Result is ObjectResult objResult)
                    {
                        multiSwapViewResult = new MultiSwapViewResult
                        {
                            Model = objResult.Value
                        };
                    }
                    else
                    {
                        multiSwapViewResult = (MultiSwapViewResult)context.Result;
                    }
                    await UpdateMultiSwapViewResultAsync(attribute, multiSwapViewResult, context);
                    context.Result = multiSwapViewResult;
                }
                else
                {
                    var viewName = await GetViewNameForNonHtmxRequest(attribute, cad);
                    var controller = (Controller)context.Controller;
                    context.Result = new ViewResult
                    {
                        ViewName = viewName,
                        ViewData = new ViewDataDictionary(controller.ViewData)
                        {
                            Model = context.Result is ObjectResult obj ? obj.Value : null
                        },
                        TempData = controller.TempData
                    };
                }
            }
        }
        await next();
    }

    /// <summary>
    /// Gets the view name for non-HTMX requests. This method should be overridden by derived classes to provide a specific view name.
    /// If this filter is not applicable to non-HTMX requests, it should be overridden to throw an exception.
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="cad"></param>
    /// <returns></returns>
    protected virtual Task<string?> GetViewNameForNonHtmxRequest(T attribute, ControllerActionDescriptor cad)
    {
        // Default implementation returns null, derived classes should override this to provide a view name.
        return Task.FromResult<string?>(null);
    }
    protected abstract Task UpdateMultiSwapViewResultAsync(T attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context);

}