using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Core.Services;
using CruSibyl.Web.Extensions;
using CruSibyl.Web.Middleware;
using CruSibyl.Web.Middleware.Auth;
using Htmx;
using Htmx.Components;
using Htmx.Components.Action;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;

#if DEBUG
Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
#endif

var builder = WebApplication.CreateBuilder(args);

var loggingSection = builder.Configuration.GetSection("Serilog");

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
    var appBuilder = WebApplication.CreateBuilder(args);
    appBuilder.Host.UseSerilog((ctx, lc) => configureLogging(lc));

    // Add services to the container.

    appBuilder.Services.AddHtmxComponents(config =>
    {
        ConfigureNav(config);
        ConfigureModelHandlers(config);
        config.WithPermissionRequirementFactory<PermissionRequirementFactory>();
        config.WithResourceOperationRegistry<ResourceOperationRegistry>();
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
        oidc.ClientId = builder.Configuration["Authentication:ClientId"];
        oidc.ClientSecret = builder.Configuration["Authentication:ClientSecret"];
        oidc.Authority = builder.Configuration["Authentication:Authority"];
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
        oidc.Events.OnTicketReceived = async context =>
        {
            if (context.Principal == null || context.Principal.Identity == null)
            {
                return;
            }
            var identity = (ClaimsIdentity)context.Principal.Identity;

            // Sometimes CAS doesn't return the required IAM ID
            // If this happens, we take the reliable Kerberos (NameIdentifier claim) and use it to lookup IAM ID
            if (!identity.HasClaim(c => c.Type == UserService.IamIdClaimType) ||
                !identity.HasClaim(c => c.Type == ClaimTypes.Surname) ||
                !identity.HasClaim(c => c.Type == ClaimTypes.GivenName) ||
                !identity.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                var identityService = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
                var kerbId = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (kerbId != null)
                {
                    Log.Error($"CAS IAM Id Missing. For Kerb: {kerbId}");
                    var identityUser = await identityService.GetByKerberos(kerbId.Value);

                    if (identityUser != null)
                    {
                        if (!identity.HasClaim(c => c.Type == UserService.IamIdClaimType))
                        {
                            identity.AddClaim(new Claim(UserService.IamIdClaimType, identityUser.Iam));
                        }
                        //Check for other missing claims
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Surname))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Surname, identityUser.LastName));
                        }
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.GivenName))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.GivenName, identityUser.FirstName));
                        }
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Email))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Email, identityUser.Email));
                        }
                    }
                    else
                    {
                        Log.Error($"IAM Id Not Found with identity service. For Kerb: {kerbId}");
                    }
                }
                else
                {
                    Log.Error($"CAS IAM Id Missing. Kerb Not Found");
                }
            }

            // Ensure user exists in the db
            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            await userService.GetUser(identity.Claims.ToArray());
        };
    });


    appBuilder.Services.AddAuthorization(options =>
    {
        options.AddAccessPolicy(AccessPolicies.SystemAccess);
        options.AddAccessPolicy(AccessPolicies.AdminAccess);
    });
    appBuilder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

    // Migration scaffolding in EF Core 8 appears to instantiate a DbContext, so we're using
    // an environment variable set by CreateMigration.sh to ensure the correct provider is used.
    var migrationUseSql = builder.Configuration.GetValue<bool?>("Migration:UseSql");
    var useSql = migrationUseSql.HasValue ? migrationUseSql.Value : builder.Configuration.GetValue<bool>("Dev:UseSql");

    if (useSql)
    {
        appBuilder.Services.AddDbContextPool<AppDbContext, AppDbContextSqlServer>((serviceProvider, o) =>
        {
            o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("CruSibyl.Core");
                });
#if DEBUG
            o.EnableSensitiveDataLogging();
#endif
        });
    }
    else
    {
        appBuilder.Services.AddDbContextPool<AppDbContext, AppDbContextSqlite>((serviceProvider, o) =>
        {
            o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly("CruSibyl.Core");
                });

#if DEBUG
            o.EnableSensitiveDataLogging();
#endif
        });
    }

    appBuilder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Authentication"));

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
        if (migrationUseSql.HasValue)
        {
            return 0;
        }
        throw;
    }

    // ensure db is up to date
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recreateDb = builder.Configuration.GetValue<bool>("Dev:RecreateDb");

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

static void ConfigureNav(HtmxComponentOptions config)
{
    config.WithNavBuilder(context =>
    {
        var path = context.HttpContext.Request.Path.ToString();

        var navBuilder = new ActionSetBuilder()
            .AddModel(m => m
                .WithLabel("Home")
                .WithIcon("fas fa-home")
                .WithHxGet("/Dashboard")
                .WithHxPushUrl())

            .AddGroup(g => g
                .WithLabel("Admin")
                .WithIcon("fas fa-cogs")
                .AddModel(m => m
                    .WithLabel("Repos")
                    .WithHxGet("/Admin")
                    .WithHxPushUrl()));

        return Task.FromResult(navBuilder);
    });
}

static void ConfigureModelHandlers(HtmxComponentOptions config)
{
    config.WithModelHandlerRegistry(registry =>
    {
        registry.Register<Repo, int>(nameof(Repo), (serviceProvider, model) =>
        {
            var typeId = nameof(Repo);
            var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
            model.WithKeySelector(r => r.Id)
                .WithQueryable(() => dbContext.Repos)
                .WithCreateModel(async repo =>
                {
                    dbContext.Repos.Add(repo);
                    await dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                })
                .WithUpdateModel(async repo =>
                {
                    dbContext.Repos.Update(repo);
                    await dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                })
                .WithDeleteModel(async id =>
                {
                    var repo = await dbContext.Repos.FindAsync(id);
                    if (repo != null)
                    {
                        dbContext.Repos.Remove(repo);
                        await dbContext.SaveChangesAsync();
                        return Htmx.Components.Models.Result.Ok();
                    }
                    return Htmx.Components.Models.Result.Error("Repo not found");
                })
                .WithTableModel(table => table
                    .WithTypeId(typeId)
                    .WithActions(table => [
                        new ActionModel("Add New")
                            .WithIcon("fas fa-plus mr-1")
                            .WithHxPost($"/Table/{typeId}/NewTableRow")
                    ])
                    .AddSelectorColumn("Name", x => x.Name, config => config
                        .WithEditable()
                        .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
                    .AddSelectorColumn("Description", x => x.Description!, config => config
                        .WithEditable())
                    .AddDisplayColumn("Actions", col =>
                    {
                        col.WithActions(row =>
                        row.IsEditing ?
                        [
                            new ActionModel("Save")
                                .WithIcon("fas fa-save") // Font Awesome 5 icon for save
                                .WithHxPost($"/Table/{typeId}/SaveRow"),

                            new ActionModel("Cancel")
                                .WithIcon("fas fa-times") // Font Awesome 5 icon for cancel
                                .WithHxPost($"/Table/{typeId}/CancelEditRow")
                        ]
                        :
                        [
                            new ActionModel("Edit")
                                .WithIcon("fas fa-edit") // Font Awesome 5 icon for edit
                                .WithHxPost($"/Table/{typeId}/EditRow?key={row.Key}"),

                            new ActionModel("Delete")
                                .WithIcon("fas fa-trash") // Font Awesome 5 icon for delete
                                .WithClass("text-red-600")
                                .WithHxPost($"/Table/{typeId}/DeleteRow?key={row.Key}")
                        ]);
                    }));
        });
    });
}