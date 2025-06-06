using System.Security.Claims;
using Htmx.Components.Authorization;
using Htmx.Components.Filters;
using Htmx.Components.Models.Builders;
using Htmx.Components.Models.Table;
using Htmx.Components.NavBar;
using Htmx.Components.Services;
using Htmx.Components.State;
using Htmx.Components.Table;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Http;
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

        services.AddSingleton(options.TableViewPaths);
        services.AddScoped<ITableProvider, TableProvider>();
        services.AddMemoryCache();
        services.Configure<AuthorizationMetadataSettings>(settings =>
        {
            settings.UserIdClaimType = options.UserIdClaimType;
        });
        services.AddScoped<IAuthorizationMetadataService, AuthorizationMetadataService>();


        if (options.NavProviderFactory is not null)
        {
            services.AddScoped<INavProvider, BuilderBasedNavProvider>(options.NavProviderFactory);
        }
        else
        {
            services.AddScoped<INavProvider, AttributeNavProvider>();
        }

        if (options.ModelRegistryFactory is not null)
        {
            services.AddScoped<IModelRegistry>(options.ModelRegistryFactory);
            services.AddScoped<IModelHandlerFactory, ModelHandlerFactory>();
            services.AddScoped<IModelHandlerFactoryGeneric, ModelHandlerFactory>();
        }

        services.AddScoped<IPageState, PageState>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddDataProtection();
        services.AddScoped<TableOobRefreshFilter>();
        services.AddScoped<PageStateOobInjectorFilter>();
        services.AddScoped<TableOobEditFilter>();
        services.AddScoped<NavActionResultFilter>();

        services.PostConfigure<MvcOptions>(options =>
        {
            // Be sure to place filters that convert models to MultiSwapViewResults before the filters that inject OOB content.
            options.Filters.AddService<TableOobRefreshFilter>();
            options.Filters.AddService<TableOobEditFilter>();
            options.Filters.AddService<NavActionResultFilter>();
            options.Filters.AddService<PageStateOobInjectorFilter>();
        });

        if (options.RegisterPermissionRequirementFactory == null
            && !services.Any(sd => sd.ServiceType == typeof(IPermissionRequirementFactory)))
        {
            throw new InvalidOperationException(
                $"{nameof(HtmxComponentOptions)}.{nameof(HtmxComponentOptions.WithPermissionRequirementFactory)}() must be called to register a permission requirement factory.");
        }
        options.RegisterPermissionRequirementFactory!(services);

        if (options.RegisterResourceOperationRegistry == null
            && !services.Any(sd => sd.ServiceType == typeof(IResourceOperationRegistry)))
        {
            throw new InvalidOperationException(
                $"{nameof(HtmxComponentOptions)}.{nameof(HtmxComponentOptions.WithResourceOperationRegistry)}() must be called to register a resource operation registry.");
        }
        options.RegisterResourceOperationRegistry!(services);

        return services;
    }

    public static IMvcBuilder AddHtmxComponentsApplicationPart(this IMvcBuilder builder)
    {
        builder.Services.AddSingleton<HtmxComponentsApplicationPartMarker>();
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }

    public static IMvcCoreBuilder AddHtmxComponentsApplicationPart(this IMvcCoreBuilder builder)
    {
        builder.Services.AddSingleton<HtmxComponentsApplicationPartMarker>();
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
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

/// <summary>
/// Marker class to ensure that AddHtmxComponentsApplicationPart() is called
/// </summary>
internal class HtmxComponentsApplicationPartMarker { }


public class HtmxComponentOptions
{
    internal Func<IServiceProvider, BuilderBasedNavProvider>? NavProviderFactory { get; private set; }
    internal TableViewPaths TableViewPaths { get; private set; } = new TableViewPaths();
    internal Func<IServiceProvider, ModelRegistry>? ModelRegistryFactory { get; private set; }
    internal Action<IServiceCollection>? RegisterPermissionRequirementFactory { get; private set; }
    internal Action<IServiceCollection>? RegisterResourceOperationRegistry { get; private set; }
    internal string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    public HtmxComponentOptions WithUserIdClaimType(string claimType)
    {
        UserIdClaimType = claimType;
        return this;
    }

    public HtmxComponentOptions WithNavBuilder(Func<ActionSetBuilder, Task> builderFactory)
    {
        NavProviderFactory = serviceProvider =>
        {
            var actionSetBuilder = new ActionSetBuilder(serviceProvider);
            return new BuilderBasedNavProvider(serviceProvider, builderFactory);
        };
        return this;
    }

    public HtmxComponentOptions WithNavBuilder(Action<ActionSetBuilder> builderFactory)
    {
        NavProviderFactory = serviceProvider =>
        {
            var funcBuilderFactory = new Func<ActionSetBuilder, Task>(builder =>
            {
                builderFactory(builder);
                return Task.CompletedTask;
            });
            return new BuilderBasedNavProvider(serviceProvider, funcBuilderFactory);
        };
        return this;
    }

    public HtmxComponentOptions WithTableOverrides(Action<TableViewPaths> configure)
    {
        configure(TableViewPaths);
        return this;
    }

    public HtmxComponentOptions WithModelHandlerRegistry(Action<IModelRegistry, IServiceProvider> configure)
    {
        ModelRegistryFactory = serviceProvider =>
        {
            var tableViewPaths = serviceProvider.GetRequiredService<TableViewPaths>();
            var resourceOperationRegistry = serviceProvider.GetRequiredService<IResourceOperationRegistry>();
            var modelRegistry = new ModelRegistry(tableViewPaths, serviceProvider, resourceOperationRegistry);
            configure(modelRegistry, serviceProvider);
            return modelRegistry;
        };
        return this;
    }

    public void WithPermissionRequirementFactory<T>()
        where T : class, IPermissionRequirementFactory
    {
        RegisterPermissionRequirementFactory = services =>
        {
            services.AddSingleton<IPermissionRequirementFactory, T>();
        };
    }

    public void WithResourceOperationRegistry<T>()
        where T : class, IResourceOperationRegistry
    {
        RegisterResourceOperationRegistry = services =>
        {
            services.AddScoped<IResourceOperationRegistry, T>();
        };
    }
}