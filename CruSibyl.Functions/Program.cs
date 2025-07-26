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
using System;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        Console.WriteLine("Configuring app configuration...");
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets<Program>();
        }
        Console.WriteLine("App configuration completed.");
    })
    .ConfigureLogging((context, logging) =>
    {
        Console.WriteLine("Configuring logging...");
        logging.ClearProviders();
        try 
        {
            logging.AddSerilog(LogConfiguration.Setup(context.Configuration));
            Console.WriteLine("Serilog configured successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Serilog configuration failed: {ex.Message}");
            // Fallback to console logging
            logging.AddConsole();
        }
    })
    .ConfigureServices((context, services) =>
    {
        try
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
            Console.WriteLine("All services registered.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service configuration failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    })
    .Build();

Console.WriteLine("Host built successfully, starting...");
host.Run();
