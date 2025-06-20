using System.Security.Claims;
using Htmx.Components.Authorization;
using Htmx.Components.AuthStatus;
using Htmx.Components.Filters;
using Htmx.Components.Models;
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

/// <summary>
/// Extension methods for registering Htmx Components services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all required Htmx Components services and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for HtmxComponentOptions.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHtmxComponents(this IServiceCollection services, Action<HtmxComponentOptions>? configure = null)
    {
        services.AddSafeActionContextAccessor(nameof(AddHtmxComponents));
        services.AddHttpContextAccessor();
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationFormats.Add("/Views/Shared/Components/Table/{0}.cshtml");
        });

        var options = new HtmxComponentOptions();
        configure?.Invoke(options);

        RegisterCoreServices(services, options);
        RegisterFilters(services);
        RegisterAuthorization(services, options);

        return services;
    }

    /// <summary>
    /// Adds the Htmx Components application part to MVC.
    /// </summary>
    public static IMvcBuilder AddHtmxComponentsApplicationPart(this IMvcBuilder builder)
    {
        builder.Services.AddSingleton<HtmxComponentsApplicationPartMarker>();
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }

    /// <summary>
    /// Adds the Htmx Components application part to MVC Core.
    /// </summary>
    public static IMvcCoreBuilder AddHtmxComponentsApplicationPart(this IMvcCoreBuilder builder)
    {
        builder.Services.AddSingleton<HtmxComponentsApplicationPartMarker>();
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }

    /// <summary>
    /// Registers IActionContextAccessor safely before MVC infrastructure.
    /// </summary>
    public static IServiceCollection AddSafeActionContextAccessor(this IServiceCollection services,
        string extensionMethodName = nameof(AddSafeActionContextAccessor))
    {
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

        services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();
        return services;
    }

    /// <summary>
    /// Marker class to ensure that AddHtmxComponentsApplicationPart() is called.
    /// </summary>
    internal class HtmxComponentsApplicationPartMarker { }

    private static void RegisterCoreServices(IServiceCollection services, HtmxComponentOptions options)
    {
        services.AddSingleton(options.ViewPaths);
        services.AddScoped<ITableProvider, TableProvider>();
        services.AddMemoryCache();

        if (options.NavProviderFactory is not null)
            services.AddScoped<INavProvider, BuilderBasedNavProvider>(options.NavProviderFactory);
        else
            services.AddScoped<INavProvider, AttributeNavProvider>();

        if (options.ModelRegistryFactory is not null)
        {
            services.AddScoped<IModelRegistry>(options.ModelRegistryFactory);
            services.AddScoped<IModelHandlerFactory, ModelHandlerFactory>();
            services.AddScoped<IModelHandlerFactoryGeneric, ModelHandlerFactory>();
        }

        services.AddScoped<IPageState, PageState>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddDataProtection();
        services.AddScoped<IAuthStatusProvider>(sp =>
            options.AuthStatusProviderFactory?.Invoke(sp) ?? new DefaultAuthStatusProvider());
    }

    private static void RegisterFilters(IServiceCollection services)
    {
        services.AddScoped<TableOobRefreshFilter>();
        services.AddScoped<PageStateOobInjectorFilter>();
        services.AddScoped<TableOobEditFilter>();
        services.AddScoped<NavActionResultFilter>();
        services.AddScoped<AuthStatusUpdateFilter>();

        services.PostConfigure<MvcOptions>(options =>
        {
            // Be sure to place filters that convert models to MultiSwapViewResults before the filters that inject OOB content.
            options.Filters.AddService<TableOobRefreshFilter>();
            options.Filters.AddService<TableOobEditFilter>();
            options.Filters.AddService<AuthStatusUpdateFilter>();
            options.Filters.AddService<NavActionResultFilter>();
            options.Filters.AddService<PageStateOobInjectorFilter>();
        });
    }

    private static void RegisterAuthorization(IServiceCollection services, HtmxComponentOptions options)
    {
        services.Configure<AuthorizationMetadataSettings>(settings =>
        {
            settings.UserIdClaimType = options.UserIdClaimType;
        });
        services.AddScoped<IAuthorizationMetadataService, AuthorizationMetadataService>();

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

        // Optional: Register role service if provided
        options.RegisterRoleService?.Invoke(services);
    }
}

/// <summary>
/// Options for configuring Htmx Components.
/// </summary>
public class HtmxComponentOptions
{
    internal Func<IServiceProvider, BuilderBasedNavProvider>? NavProviderFactory { get; private set; }
    internal ViewPaths ViewPaths { get; private set; } = new ViewPaths();
    internal Func<IServiceProvider, ModelRegistry>? ModelRegistryFactory { get; private set; }
    internal Action<IServiceCollection>? RegisterPermissionRequirementFactory { get; private set; }
    internal Action<IServiceCollection>? RegisterResourceOperationRegistry { get; private set; }
    internal Action<IServiceCollection>? RegisterRoleService { get; set; }
    internal string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;
    internal Func<IServiceProvider, IAuthStatusProvider> AuthStatusProviderFactory { get; set; } = sp =>
        new DefaultAuthStatusProvider();
    

    public HtmxComponentOptions WithAuthStatusProvider(Func<IServiceProvider, IAuthStatusProvider> factory)
    {
        AuthStatusProviderFactory = factory;
        return this;
    }

    public HtmxComponentOptions WithRoleService<T>()
            where T : class, IRoleService
    {
        RegisterRoleService = services =>
        {
            services.AddScoped<IRoleService, T>();
        };
        return this;
    }

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

    public HtmxComponentOptions WithViewOverrides(Action<ViewPaths> configure)
    {
        configure(ViewPaths);
        return this;
    }

    public HtmxComponentOptions WithModelHandlerRegistry(Action<IModelRegistry, IServiceProvider> configure)
    {
        ModelRegistryFactory = serviceProvider =>
        {
            var viewPaths = serviceProvider.GetRequiredService<ViewPaths>();
            var resourceOperationRegistry = serviceProvider.GetRequiredService<IResourceOperationRegistry>();
            var modelRegistry = new ModelRegistry(viewPaths, serviceProvider, resourceOperationRegistry);
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