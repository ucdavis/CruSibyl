using System.Diagnostics;
using CruSibyl.Core.Data;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Core.Services;
using CruSibyl.Web.Configuration;
using CruSibyl.Web.Extensions;
using CruSibyl.Web.Middleware;
using CruSibyl.Web.Middleware.Auth;
using Htmx.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
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
        .Enrich.WithExceptionDetails()
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
        htmxOptions.WithNavBuilder(NavConfig.RegisterNavigation);
        htmxOptions.WithModelHandlerRegistry(ModelHandlerConfig.RegisterModels);
        htmxOptions.WithPermissionRequirementFactory<PermissionRequirementFactory>();
        htmxOptions.WithResourceOperationRegistry<ResourceOperationRegistry>();
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
    });
    appBuilder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

    DBContextConfig.Configure(appBuilder, out var migrationScaffoldRequested);

    appBuilder.Services.Configure<AuthSettings>(appBuilder.Configuration.GetSection("Authentication"));

    appBuilder.Services.AddScoped<IIdentityService, IdentityService>();
    appBuilder.Services.AddScoped<IUserService, UserService>();
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



