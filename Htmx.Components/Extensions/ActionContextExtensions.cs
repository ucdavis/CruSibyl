using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Htmx.Components.Extensions;

public static class ActionContextExtensions
{
    /// <summary>
    /// Provides a sanity check to fail early if IActionContextAccessor is not initialized
    /// </summary>
    public static ActionContext GetValidActionContext(this IActionContextAccessor actionContextAccessor)
    {
        if (actionContextAccessor.ActionContext?.HttpContext == null)
            throw new InvalidOperationException("HttpContext is not available.");
        if (actionContextAccessor.ActionContext?.RouteData == null)
            throw new InvalidOperationException("RouteData is not available.");
        if (actionContextAccessor.ActionContext?.ActionDescriptor == null)
            throw new InvalidOperationException("ActionDescriptor is not available.");

        return actionContextAccessor.ActionContext;
    }
}