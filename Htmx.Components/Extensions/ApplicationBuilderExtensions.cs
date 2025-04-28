using Htmx.Components.State;
using Microsoft.AspNetCore.Builder;

namespace Htmx.Components;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseHtmxGlobalState(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalStateMiddleware>();
        return app;
    }
}