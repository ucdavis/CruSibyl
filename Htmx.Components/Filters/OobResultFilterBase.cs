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
            if (attribute != null && (
                context.Result is ObjectResult
                || context.Result is MultiSwapViewResult
                || context.Result is OkResult))
            {
                if (context.HttpContext.Request.IsHtmx())
                {
                    MultiSwapViewResult multiSwapViewResult = null!;
                    multiSwapViewResult = context.Result switch
                    {
                        ObjectResult objResult => new MultiSwapViewResult
                        {
                            Model = objResult.Value
                        },
                        // TODO: OkResult is a special case that isn't applicable to all subclasses. Find a way to handle this more gracefully.
                        OkResult => new MultiSwapViewResult(),
                        MultiSwapViewResult msvr => msvr,
                        _ => throw new InvalidOperationException("Unsupported result type.")
                    };
                    await UpdateMultiSwapViewResultAsync(attribute, multiSwapViewResult, context);
                    context.Result = multiSwapViewResult;
                }
                else
                {
                    // TODO: OkResult is a special case that isn't applicable to all subclasses. Find a way to handle this more gracefully.
                    if (context.Result is OkResult)
                    {
                        await next();
                        return;
                    }
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

//TODO: If the number of subclasses or special edge cases balloons, consider a more generic pluggable system
// public interface IMultiSwapViewResultMutator
// {
//     Task MutateAsync(ResultExecutingContext context, MultiSwapViewResult result);
// }
//
// public class HtmxPipelineResultFilter : IAsyncResultFilter
// {
//     private readonly IEnumerable<IMultiSwapViewResultMutator> _mutators;
//     public HtmxPipelineResultFilter(IEnumerable<IMultiSwapViewResultMutator> mutators)
//         => _mutators = mutators;
//     public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
//     {
//         await next();
//         if (context.Result is MultiSwapViewResult result)
//         {
//             foreach (var mutator in _mutators)
//                 await mutator.MutateAsync(context, result);
//         }
//     }
// }