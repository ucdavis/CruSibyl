using System.Net;
using System.Security.Claims;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Services;
using CruSibyl.Web.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CruSibyl.Tests;

public class AuthenticationAndHealthTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Auth:Instance"] = "https://login.microsoftonline.com/",
        ["Auth:TenantId"] = "a8046f64-66c0-4f00-9046-c8daf92ff62b",
        ["Auth:ClientId"] = "11111111-1111-1111-1111-111111111111",
        ["Auth:ClientSecret"] = "test-only-not-a-real-secret",
        ["Auth:IamKey"] = "test-only",
        ["Auth:CallbackPath"] = "/signin-oidc"
    }).Build();

    private static ServiceProvider Services(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration ?? Configuration());
        services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        services.AddLogging();
        services.AddSingleton<IUserService, SignInUsers>();
        services.AddEntraAuthentication(configuration ?? Configuration());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task HtmxChallengeUsesExistingPopupProtocol()
    {
        using var services = Services();
        var options = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers["HX-Request"] = "true";
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));
        var context = new RedirectContext(http, scheme, options, new AuthenticationProperties()) { ProtocolMessage = new OpenIdConnectMessage() };
        await options.Events.OnRedirectToIdentityProvider(context);
        Assert.Equal(401, http.Response.StatusCode);
        Assert.Equal("popup-login:/auth/popup-login", http.Response.Headers["X-Auth-Failure"]);
        Assert.True(context.Handled);
    }

    [Fact]
    public async Task FullPageChallengePreservesRedirectAndDomainHint()
    {
        using var services = Services();
        var options = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var http = new DefaultHttpContext { RequestServices = services };
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));
        var context = new RedirectContext(http, scheme, options, new AuthenticationProperties()) { ProtocolMessage = new OpenIdConnectMessage() };
        await options.Events.OnRedirectToIdentityProvider(context);
        Assert.False(context.Handled);
        Assert.Equal("ucdavis.edu", context.ProtocolMessage.DomainHint);
    }

    [Fact]
    public async Task ForbiddenHtmxRequestReturns403WithoutRedirect()
    {
        using var services = Services();
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers["HX-Request"] = "true";
        var context = new RedirectContext<CookieAuthenticationOptions>(http,
            new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)), options, new AuthenticationProperties(), "/denied");
        await options.Events.OnRedirectToAccessDenied(context);
        Assert.Equal(403, http.Response.StatusCode);
        Assert.Equal("auth-denied", http.Response.Headers["HX-Trigger"]);
        Assert.Equal(".CruSibyl.Entra", options.Cookie.Name);
    }

    [Fact]
    public async Task DatabaseHealthReflectsConnectivityWithoutDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new AppDbContextSqlite(new DbContextOptionsBuilder<AppDbContextSqlite>().UseSqlite(connection).Options);
        var check = new DatabaseHealthCheck(db);
        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status);
        await using var unavailable = new AppDbContextSqlite(new DbContextOptionsBuilder<AppDbContextSqlite>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.db")};Mode=ReadOnly").Options);
        var failed = await new DatabaseHealthCheck(unavailable).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, failed.Status);
        Assert.Null(failed.Exception);
        Assert.Null(failed.Description);
    }

    [Fact]
    public async Task TrustedProxyRestoresHttpsScheme()
    {
        using var services = Services();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
            Options.Create(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto }));
        await middleware.Invoke(context);
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public void CookieKeysSurviveProviderRestartAndAreEnvironmentIsolated()
    {
        var directory = Directory.CreateTempSubdirectory("crusibyl-keys-");
        try
        {
            ServiceProvider Provider(string environment)
            {
                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    { ["HOME"] = directory.FullName, ["WEBSITE_INSTANCE_ID"] = "test-worker" }).Build();
                var services = new ServiceCollection().AddLogging();
                services.AddOperationalServices(config, new TestEnvironment { EnvironmentName = environment });
                return services.BuildServiceProvider();
            }
            using var first = Provider("Staging");
            var cookie = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Protect("principal");
            using var restarted = Provider("Staging");
            Assert.Equal("principal", restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Unprotect(cookie));
            using var production = Provider("Production");
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => production.GetRequiredService<IDataProtectionProvider>().CreateProtector("auth-cookie").Unprotect(cookie));
            Assert.True(Directory.Exists(Path.Combine(directory.FullName, "ASP.NET", "DataProtection-Keys", "Staging")));
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task TokenValidationRequiresIamBeforeApplicationSignIn()
    {
        using var services = Services();
        var options = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));
        var context = new TokenValidatedContext(new DefaultHttpContext { RequestServices = services }, scheme, options, new ClaimsPrincipal(new ClaimsIdentity([], "Entra")), new AuthenticationProperties());
        await options.Events.OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);

        var valid = new TokenValidatedContext(new DefaultHttpContext { RequestServices = services }, scheme, options,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(UserService.IamIdClaimType, "1000255034")], "Entra")), new AuthenticationProperties());
        await options.Events.OnTokenValidated(valid);
        Assert.Null(valid.Result?.Failure);
    }

    private sealed class SignInUsers : IUserService
    {
        public Task<User?> GetUserAsync(Claim[] claims, bool includePermissions = false) => Task.FromResult<User?>(new User { Iam = UserService.RequireIamId(claims) });
        public Task<User?> GetUser(string query, bool includePermissions = false) => throw new NotSupportedException();
        public Task<User?> GetCurrentUserAsync(bool includePermissions = false) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetCurrentPermissionsAsync() => throw new NotSupportedException();
        public string? GetCurrentUserId() => throw new NotSupportedException();
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "CruSibyl.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
