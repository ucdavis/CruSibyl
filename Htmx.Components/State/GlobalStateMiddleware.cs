using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Htmx.Components.State;

public class GlobalStateMiddleware
{
    private readonly RequestDelegate _next;
    internal const string HttpContextGlobalStateKey = "GlobalState";
    internal const string GlobalStateHeaderKey = "X-Global-State";

    public GlobalStateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGlobalStateManager globalStateManager)
    {
        string? encryptedState = null;

        if (context.Request.Headers.TryGetValue(GlobalStateHeaderKey, out var headerValue))
        {
            encryptedState = headerValue.FirstOrDefault();
        }

        if (!string.IsNullOrEmpty(encryptedState))
        {
            globalStateManager.Load(encryptedState);
        }

        // Attach to HttpContext
        context.Items[HttpContextGlobalStateKey] = globalStateManager;

        await _next(context);
    }

    public static IGlobalStateManager GetGlobalState(HttpContext context)
    {
        return context.Items[HttpContextGlobalStateKey] as IGlobalStateManager 
            ?? throw new InvalidOperationException("Global state not available");
    }
}
