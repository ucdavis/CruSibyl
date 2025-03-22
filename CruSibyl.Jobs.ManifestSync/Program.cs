using System;
using System.Threading.Tasks;
using CruSibyl.Core.Data;
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

                var syncService = provider.GetRequiredService<IManifestSyncService>();

                var result = SyncManifests(syncService).GetAwaiter().GetResult();

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

            var efProvider = Configuration.GetValue("Provider", "none");
            if (efProvider == "SqlServer" || (efProvider == "none" && Configuration.GetValue<bool>("Dev:UseSql")))
            {
                services.AddDbContextPool<AppDbContext, AppDbContextSqlServer>((serviceProvider, o) =>
                {
                    o.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"),
                        sqlOptions =>
                        {
                            sqlOptions.MigrationsAssembly("CruSibyl.Core");
                        });
#if DEBUG
                    o.EnableSensitiveDataLogging();
                    o.LogTo(message => System.Diagnostics.Debug.WriteLine(message));
#endif
                });
            }
            else
            {
                services.AddDbContextPool<AppDbContext, AppDbContextSqlite>((serviceProvider, o) =>
                {
                    var connection = new SqliteConnection("Data Source=crusibyl.db");
                    o.UseSqlite(connection, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly("CruSibyl.Core");
                    });

#if DEBUG
                    o.EnableSensitiveDataLogging();
                    o.LogTo(message => System.Diagnostics.Debug.WriteLine(message));
#endif
                });
            }
            services.AddMemoryCache();
            services.Configure<GitHubSettings>(Configuration.GetSection("GitHub"));
            services.AddSingleton<IGitHubService, GitHubService>();
            services.AddSingleton<IManifestSyncService, ManifestSyncService>();


            return services.BuildServiceProvider();
        }

        private static async Task<Result> SyncManifests(IManifestSyncService syncService)
        {
            Log.Information("Syncing repository manifests");

            return await syncService.SyncManifests();
        }
    }
}
