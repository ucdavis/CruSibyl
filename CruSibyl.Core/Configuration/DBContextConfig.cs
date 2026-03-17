using CruSibyl.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CruSibyl.Core.Configuration;

public static class DBContextConfig
{
    public static void Configure(IConfiguration configuration, IServiceCollection services, out bool migrationScaffoldRequested)
    {
        // Migration scaffolding in EF Core 8 appears to instantiate a DbContext, so we're using
        // an environment variable set by CreateMigration.sh to opt into Sqlite when needed.
        var migrationUseSqlite = configuration.GetValue<bool?>("Migration:UseSqlite");
        var useSqlite = migrationUseSqlite.HasValue ? migrationUseSqlite.Value : configuration.GetValue<bool>("Dev:UseSqlite");

        if (useSqlite)
        {
            services.AddDbContextPool<AppDbContext, AppDbContextSqlite>(ConfigSqlite(configuration));
            services.AddDbContextFactory<AppDbContextSqlite>(ConfigSqlite(configuration));
            services.AddScoped<IDbContextFactory<AppDbContext>>(sp =>
                new DbContextFactoryAdapter<AppDbContextSqlite>(sp.GetRequiredService<IDbContextFactory<AppDbContextSqlite>>()));
        }
        else
        {
            services.AddDbContextPool<AppDbContext, AppDbContextSqlServer>(ConfigSqlServer(configuration));
            services.AddDbContextFactory<AppDbContextSqlServer>(ConfigSqlServer(configuration));
            services.AddScoped<IDbContextFactory<AppDbContext>>(sp =>
                new DbContextFactoryAdapter<AppDbContextSqlServer>(sp.GetRequiredService<IDbContextFactory<AppDbContextSqlServer>>()));
        }

        // A null value indicates that no migration scaffold has been requested.
        migrationScaffoldRequested = migrationUseSqlite.HasValue;
    }

    static Action<IServiceProvider, DbContextOptionsBuilder> ConfigSqlServer(IConfiguration configuration)
    {
        return (serviceProvider, o) =>
        {
            o.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("CruSibyl.Core");
                });
#if DEBUG
            o.EnableSensitiveDataLogging();
#endif
        };
    }

    static Action<IServiceProvider, DbContextOptionsBuilder> ConfigSqlite(IConfiguration configuration)
    {
        return (serviceProvider, o) =>
        {
            o.UseSqlite(configuration.GetConnectionString("DefaultConnection"),
                sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly("CruSibyl.Core");
                });

#if DEBUG
            o.EnableSensitiveDataLogging();
#endif
        };
    }
}
