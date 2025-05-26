using CruSibyl.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Web.Configuration;

public static class DBContextConfig
{
    public static void Configure(WebApplicationBuilder appBuilder, out bool migrationScaffoldRequested)
    {
        // Migration scaffolding in EF Core 8 appears to instantiate a DbContext, so we're using
        // an environment variable set by CreateMigration.sh to ensure the correct provider is used.
        var migrationUseSql = appBuilder.Configuration.GetValue<bool?>("Migration:UseSql");
        var useSql = migrationUseSql.HasValue ? migrationUseSql.Value : appBuilder.Configuration.GetValue<bool>("Dev:UseSql");

        if (useSql)
        {
            appBuilder.Services.AddDbContextPool<AppDbContext, AppDbContextSqlServer>(ConfigSqlServer(appBuilder));
            appBuilder.Services.AddDbContextFactory<AppDbContextSqlServer>(ConfigSqlServer(appBuilder));
            appBuilder.Services.AddScoped<IDbContextFactory<AppDbContext>>(sp =>
                new DbContextFactoryAdapter<AppDbContextSqlServer>(sp.GetRequiredService<IDbContextFactory<AppDbContextSqlServer>>()));
        }
        else
        {
            appBuilder.Services.AddDbContextPool<AppDbContext, AppDbContextSqlite>(ConfigSqlite(appBuilder));
            appBuilder.Services.AddDbContextFactory<AppDbContextSqlite>(ConfigSqlite(appBuilder));
            appBuilder.Services.AddScoped<IDbContextFactory<AppDbContext>>(sp =>
                new DbContextFactoryAdapter<AppDbContextSqlite>(sp.GetRequiredService<IDbContextFactory<AppDbContextSqlite>>()));
        }

        // A null value indicates that no migration scaffold has been requested.
        migrationScaffoldRequested = migrationUseSql.HasValue;
    }

    static Action<IServiceProvider, DbContextOptionsBuilder> ConfigSqlServer(WebApplicationBuilder builder)
    {
        return (serviceProvider, o) =>
        {
            o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("CruSibyl.Core");
                });
#if DEBUG
            o.EnableSensitiveDataLogging();
#endif
        };
    }

    static Action<IServiceProvider, DbContextOptionsBuilder> ConfigSqlite(WebApplicationBuilder builder)
    {
        return (serviceProvider, o) =>
        {
            o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"),
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