using CruSibyl.Core.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Web.Extensions;

public static class OperationalExtensions
{
    public static IServiceCollection AddOperationalServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var azureHosted = !string.IsNullOrEmpty(configuration["WEBSITE_INSTANCE_ID"]);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            if (azureHosted)
            {
                // App Service's platform ingress is the sole immediate proxy to the worker.
                // Do not enable this trust policy for a host exposed directly to the Internet.
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });
        var keyRoot = azureHosted
            ? Path.Combine(configuration["HOME"] ?? throw new InvalidOperationException("App Service HOME is missing."), "ASP.NET", "DataProtection-Keys")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CruSibyl", "DataProtection-Keys");
        var keys = new DirectoryInfo(Path.Combine(keyRoot, environment.EnvironmentName));
        services.AddDataProtection().SetApplicationName($"CruSibyl:{environment.EnvironmentName}").PersistKeysToFileSystem(keys);
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}

public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connected = await db.Database.CanConnectAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return connected ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
