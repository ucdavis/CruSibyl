using Htmx.Components.State;
using Microsoft.AspNetCore.Builder;

namespace Htmx.Components;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseHtmxPageState(this IApplicationBuilder app)
    {
        app.UseMiddleware<PageStateMiddleware>();
        return app;
    }
}