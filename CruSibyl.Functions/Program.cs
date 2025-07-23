// ...existing code...
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CruSibyl.Core.Services;
using CruSibyl.Core.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using CruSibyl.Functions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets<Program>();
        }
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddSerilog(LogConfiguration.Setup(context.Configuration));
    })
    .ConfigureServices((context, services) =>
    {
        services.AddOptions();
        DBContextConfig.Configure(context.Configuration, services, out var _);
        services.AddMemoryCache();
        services.Configure<GitHubSettings>(context.Configuration.GetSection("GitHub"));
        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddSingleton<IManifestSyncService, ManifestSyncService>();
        services.AddSingleton<IPackageVersionSyncService, PackageVersionSyncService>();
        services.AddSingleton<INuGetService, NuGetService>();
        services.AddSingleton<INpmService, NpmService>();
    })
    .Build();

host.Run();
