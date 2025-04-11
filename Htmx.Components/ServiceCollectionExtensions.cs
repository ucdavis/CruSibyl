using Htmx.Components.Action;
using Htmx.Components.NavBar;
using Htmx.Components.Table;
using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Htmx.Components;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHtmxComponents(this IServiceCollection services, Action<HtmxComponentOptions>? configure = null)
    {
        services.AddSafeActionContextAccessor(nameof(AddHtmxComponents));
        services.AddHttpContextAccessor();
        services.AddTransient<HtmxResultBuilder>();
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationFormats.Add("/Views/Shared/Components/Table/{0}.cshtml");
        });

        var options = new HtmxComponentOptions();
        configure?.Invoke(options);

        services.AddSingleton(options.TableViews);
        services.AddScoped<ITableProvider, TableProvider>();

        if (options.NavBuilderFactory is not null)
        {
            services.AddScoped<INavProvider, BuilderBasedNavProvider>(sp =>
            {
                var accessor = sp.GetRequiredService<IActionContextAccessor>();
                return new BuilderBasedNavProvider(accessor, options.NavBuilderFactory!);
            });
        }

        return services;
    }

    public static IServiceCollection AddSafeActionContextAccessor(this IServiceCollection services,
        string extensionMethodName = nameof(AddSafeActionContextAccessor))
    {
        // IActionContextAccessor needs to be registered prior to MVC infrastructure in order to be properly initialized.
        // Look for a few known MVC types that imply AddMvc() or AddControllers() has already run
        bool mvcAlreadyRegistered = services.Any(sd =>
            sd.ServiceType.FullName?.StartsWith("Microsoft.AspNetCore.Mvc.Infrastructure") == true ||
            sd.ServiceType == typeof(Microsoft.AspNetCore.Mvc.Infrastructure.IActionInvokerFactory) ||
            sd.ImplementationType?.FullName?.StartsWith("Microsoft.AspNetCore.Mvc") == true
        );

        if (mvcAlreadyRegistered)
        {
            throw new InvalidOperationException(
                $"IActionContextAccessor must be registered before MVC. " +
                $"Call {extensionMethodName}() before AddControllers() or AddMvc()."
            );
        }

        // There isn't an equivalent of AddHttpContextAccessor for IActionContextAccessor, but it just amounts to this...
        services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();

        return services;
    }
}

public class HtmxComponentOptions
{
    internal Func<ActionContext, Task<ActionSetBuilder>>? NavBuilderFactory { get; private set; }
    internal TableViewPaths TableViews { get; } = new();

    public HtmxComponentOptions WithNavBuilder(Func<ActionContext, Task<ActionSetBuilder>> builderFactory)
    {
        NavBuilderFactory = builderFactory;
        return this;
    }

    public HtmxComponentOptions WithTableOverrides(Action<TableViewPaths> configure)
    {
        configure(TableViews);
        return this;
    }
}