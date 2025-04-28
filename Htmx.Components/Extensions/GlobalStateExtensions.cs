using Htmx.Components.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components;

public static class GlobalStateExtensions
{
    public static IGlobalStateManager GetGlobalState(this HttpContext context)
    {
        if (context.Items.TryGetValue(GlobalStateMiddleware.HttpContextGlobalStateKey, out var value) && value is IGlobalStateManager manager)
            return manager;
        throw new InvalidOperationException("GlobalStateManager not found. Is GlobalStateMiddleware registered?");
    }

    public static IGlobalStateManager GetGlobalState(this Controller controller)
        => controller.HttpContext.GetGlobalState();
}
