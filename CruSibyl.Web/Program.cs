using System.Diagnostics;
using CruSibyl.Core.Configuration;
using CruSibyl.Core.Data;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Core.Services;
using CruSibyl.Web.Extensions;
using CruSibyl.Web.Middleware;
using CruSibyl.Web.Middleware.Auth;
using CruSibyl.Web.Services;
using Htmx.Components;
using Htmx.Components.Configuration;
using Htmx.Components.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Sinks.Elasticsearch;

#if DEBUG
Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
#endif

var appBuilder = WebApplication.CreateBuilder(args);

var loggingSection = appBuilder.Configuration.GetSection("Serilog");

// configure logging as delegate so it can be applied to both Log.Logger and appBuilder.Host.UseSerilog()
var configureLogging = (LoggerConfiguration cfg) =>
{
    cfg.MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        // .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning) // uncomment this to hide EF core general info logs
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With()
        .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
            .WithDefaultDestructurers()
            .WithDestructurers([new DbUpdateExceptionDestructurer()]))
        .Enrich.WithProperty("Application", loggingSection.GetValue<string>("AppName"))
        .Enrich.WithProperty("AppEnvironment", loggingSection.GetValue<string>("Environment"))
        .WriteTo.Console();

    // add in elastic search sink if the uri is valid
    if (Uri.TryCreate(loggingSection.GetValue<string>("ElasticUrl"), UriKind.Absolute, out var elasticUri))
    {
        cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(elasticUri)
        {
            IndexFormat = "aspnet-crusibyl-{0:yyyy.MM}",
            TypeName = null,
        });
    }

    return cfg;
};

Log.Logger = configureLogging(new LoggerConfiguration()).CreateBootstrapLogger();

try
{
    Log.Information("Starting web host");
    appBuilder.Host.UseSerilog((ctx, lc) => configureLogging(lc));

    // Add services to the container.

    appBuilder.Services.AddHtmxComponents(htmxOptions =>
    {
        // htmxOptions.WithNavBuilder(NavConfig.RegisterNavigation);
        htmxOptions.WithModelHandlerRegistry(
            // ModelRegistryConfig.RegisterModels
            (registry, serviceProvider) =>
            {
                ModelHandlerAttributeRegistrar.RegisterAll(registry);
            }
        );
        htmxOptions.WithAuthorizationRequirementFactory<PermissionRequirementFactory>();
        htmxOptions.WithResourceOperationRegistry<ResourceOperationRegistry>();
        htmxOptions.WithUserIdClaimType(UserService.IamIdClaimType);
    });

    appBuilder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<SerilogControllerActionFilter>();
    })
    .AddHtmxComponentsApplicationPart();

    appBuilder.Services.AddEndpointsApiExplorer();
    appBuilder.Services.AddSwaggerGen();
    appBuilder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(oidc =>
    {
        oidc.ClientId = appBuilder.Configuration["Authentication:ClientId"];
        oidc.ClientSecret = appBuilder.Configuration["Authentication:ClientSecret"];
        oidc.Authority = appBuilder.Configuration["Authentication:Authority"];
        oidc.ResponseType = OpenIdConnectResponseType.Code;
        oidc.Scope.Add("openid");
        oidc.Scope.Add("profile");
        oidc.Scope.Add("email");
        oidc.Scope.Add("eduPerson");
        oidc.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
        };
        oidc.ConfigureHtmxAuthPopup("/auth/popup-login");
        oidc.AddIamFallback();
    });


    appBuilder.Services.AddAuthorization(options =>
    {
        options.AddAccessPolicy(AccessPolicies.SystemAccess);
        options.AddAccessPolicy(AccessPolicies.AdminAccess);
        options.AddPolicy("DevelopmentOnly", policy =>
            policy.Requirements.Add(new DevelopmentEnvironmentRequirement()));
    });
    appBuilder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
    appBuilder.Services.AddSingleton<IAuthorizationHandler, DevelopmentEnvironmentHandler>();

    DBContextConfig.Configure(appBuilder.Configuration, appBuilder.Services, out var migrationScaffoldRequested);

    appBuilder.Services.Configure<AuthSettings>(appBuilder.Configuration.GetSection("Authentication"));

    appBuilder.Services.AddScoped<IIdentityService, IdentityService>();
    appBuilder.Services.AddScoped<IUserService, UserService>();
    appBuilder.Services.AddScoped<IDashboardService, DashboardService>();
    appBuilder.Services.AddHttpContextAccessor();


    WebApplication app = null!;

    try
    {
        app = appBuilder.Build();
    }
    catch (HostAbortedException)
    {
        // swallow exception and return early when generating a new migration
        if (migrationScaffoldRequested)
        {
            return 0;
        }
        throw;
    }

    // ensure db is up to date
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recreateDb = appBuilder.Configuration.GetValue<bool>("Dev:RecreateDb");

        if (recreateDb)
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }

        dbContext.Database.Migrate();

        var initializeUsers = app.Configuration.GetValue<bool>("Dev:InitializeUsers");
        var initializer = new DbInitializer(dbContext);

        if (initializeUsers)
        {
            initializer.InitializeUsers(recreateDb).GetAwaiter().GetResult();
        }
        else
        {
            initializer.CheckAndCreateRoles().GetAwaiter().GetResult();
        }
    }


    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }


    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseHtmxPageState();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // app.UseMiddleware<LogUserNameMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/dev/htmx-errors/{status:int}", (int status) =>
        {
            var error = status switch
            {
                StatusCodes.Status400BadRequest => new HtmxErrorFragment(
                    "Check your entry",
                    "This development-only endpoint returned a validation error.",
                    HtmxErrorKinds.Validation),
                StatusCodes.Status401Unauthorized => new HtmxErrorFragment(
                    "Sign in required",
                    "Please sign in and try again.",
                    HtmxErrorKinds.Authorization),
                StatusCodes.Status403Forbidden => new HtmxErrorFragment(
                    "Access denied",
                    "You do not have permission to perform this action.",
                    HtmxErrorKinds.Authorization),
                StatusCodes.Status500InternalServerError => throw new InvalidOperationException("Development-only HTMX 500 trigger."),
                _ => new HtmxErrorFragment(
                    "Request failed",
                    "This development-only endpoint returned an error.",
                    HtmxErrorKinds.General)
            };

            return Results.Content(error.ToHtml(), "text/html", statusCode: status);
        });

        app.MapGet("/dev/htmx-errors/{kind}", (string kind) =>
        {
            var (status, error) = kind.ToLowerInvariant() switch
            {
                "validation" => (StatusCodes.Status400BadRequest, new HtmxErrorFragment(
                    "Check your entry",
                    "This development-only endpoint returned a validation error.",
                    HtmxErrorKinds.Validation)),
                "crud" => (StatusCodes.Status400BadRequest, new HtmxErrorFragment(
                    "Request not completed",
                    "This development-only endpoint returned a CRUD operation error.",
                    HtmxErrorKinds.Crud)),
                "missing-handler" => (StatusCodes.Status400BadRequest, new HtmxErrorFragment(
                    "Request not available",
                    "This development-only endpoint returned a missing handler error.",
                    HtmxErrorKinds.MissingHandler)),
                "unauthorized" => (StatusCodes.Status401Unauthorized, new HtmxErrorFragment(
                    "Sign in required",
                    "Please sign in and try again.",
                    HtmxErrorKinds.Authorization)),
                "forbidden" => (StatusCodes.Status403Forbidden, new HtmxErrorFragment(
                    "Access denied",
                    "You do not have permission to perform this action.",
                    HtmxErrorKinds.Authorization)),
                "server" => throw new InvalidOperationException("Development-only HTMX server error trigger."),
                _ => (StatusCodes.Status400BadRequest, new HtmxErrorFragment(
                    "Request failed",
                    "This development-only endpoint returned an unknown test error.",
                    HtmxErrorKinds.General))
            };

            return Results.Content(error.ToHtml(), "text/html", statusCode: status);
        });

        app.MapGet("/dev/htmx-errors/timeout", async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            return Results.Content("Delayed development-only response.", "text/plain");
        });
    }

    // default for MVC server-side endpoints
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller}/{action}/{id?}",
        defaults: new { controller = "Dashboard", action = "Index" },
        constraints: new { controller = "(dashboard|admin)" }
    );

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

return 0;
