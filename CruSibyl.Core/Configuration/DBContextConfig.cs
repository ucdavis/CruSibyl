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
        // an environment variable set by CreateMigration.sh to ensure the correct provider is used.
        var migrationUseSql = configuration.GetValue<bool?>("Migration:UseSql");
        var useSql = migrationUseSql.HasValue ? migrationUseSql.Value : configuration.GetValue<bool>("Dev:UseSql");

        if (useSql)
        {
            services.AddDbContextPool<AppDbContext, AppDbContextSqlServer>(ConfigSqlServer(configuration));
            services.AddDbContextFactory<AppDbContextSqlServer>(ConfigSqlServer(configuration));
            services.AddScoped<IDbContextFactory<AppDbContext>>(sp =>
                new DbContextFactoryAdapter<AppDbContextSqlServer>(sp.GetRequiredService<IDbContextFactory<AppDbContextSqlServer>>()));
        }
        else
        {
            services.AddDbContextPool<AppDbContext, AppDbContextSqlite>(ConfigSqlite(configuration));
            services.AddDbContextFactory<AppDbContextSqlite>(ConfigSqlite(configuration));
            services.AddScoped<IDbContextFactory<AppDbContext>>(sp =>
                new DbContextFactoryAdapter<AppDbContextSqlite>(sp.GetRequiredService<IDbContextFactory<AppDbContextSqlite>>()));
        }

        // A null value indicates that no migration scaffold has been requested.
        migrationScaffoldRequested = migrationUseSql.HasValue;
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