using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Htmx.Components.State;

public class GlobalStateMiddleware
{
    private readonly RequestDelegate _next;
    private const string GlobalStateFormKey = "global_state";
    internal const string HttpContextGlobalStateKey = "GlobalState";

    public GlobalStateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGlobalStateManager globalStateManager)
    {
        string? encryptedState = null;

        if (context.Request.Headers.TryGetValue("X-Global-State", out var headerValue))
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

    public static GlobalStateManager GetGlobalState(HttpContext context)
    {
        return context.Items[HttpContextGlobalStateKey] as GlobalStateManager 
               ?? throw new InvalidOperationException("Global state not available");
    }
}
