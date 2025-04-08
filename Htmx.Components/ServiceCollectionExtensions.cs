using Htmx.Components.Action;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Htmx.Components;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHtmxComponents(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // Register services related to HTMX components
        services.AddScoped<HtmxOobHelper>();

        return services;
    }

    public static IServiceCollection AddBuilderNavProvider(this IServiceCollection services,
        Func<ActionContext, Task<ActionSetBuilder>> builderFactory)
    {
        // There isn't an equivalent of AddHttpContextAccessor for IActionContextAccessor, but it just amounts to this...
        services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();
        
        services.AddScoped<INavProvider, BuilderBasedNavProvider>(sp =>
        {
            var accessor = sp.GetRequiredService<IActionContextAccessor>();

            return new BuilderBasedNavProvider(accessor, builderFactory);
        });

        return services;
    }
}
