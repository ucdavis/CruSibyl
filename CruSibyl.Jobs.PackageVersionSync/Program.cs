using System;
using System.Threading.Tasks;
using CruSibyl.Core.Configuration;
using CruSibyl.Core.Data;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Core.Services;
using CruSibyl.Jobs.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CruSibyl.Jobs.ManifestSync
{
    class Program : JobBase
    {
        static int Main(string[] args)
        {
            try
            {
                Configure(jobName: typeof(Program).Assembly.GetName().Name, jobId: Guid.NewGuid());
                var assembyName = typeof(Program).Assembly.GetName();

                Log.Information("Running {job} build {build}", assembyName.Name, assembyName.Version);

                // setup di
                var provider = ConfigureServices();

                var syncService = provider.GetRequiredService<IPackageVersionSyncService>();

                var result = SyncPackageVersions(syncService).GetAwaiter().GetResult();

                if (result.IsError)
                {
                    Log.Error("There was an error syncing repository manifests. See previous log entries for details.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }


        private static ServiceProvider ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddOptions();

            DBContextConfig.Configure(Configuration, services, out var _);
            services.AddMemoryCache();
            services.AddSingleton<IPackageVersionSyncService, PackageVersionSyncService>();
            services.AddSingleton<INuGetService, NuGetService>();
            services.AddSingleton<INpmService, NpmService>();


            return services.BuildServiceProvider();
        }

        private static async Task<Result> SyncPackageVersions(IPackageVersionSyncService syncService)
        {
            Log.Information("Syncing package versions");

            return await syncService.SyncPackageVersionsAsync();
        }
    }
}
