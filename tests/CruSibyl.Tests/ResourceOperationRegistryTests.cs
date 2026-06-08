using CruSibyl.Core.Data;
using CruSibyl.Web.Middleware.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CruSibyl.Tests;

public class ResourceOperationRegistryTests
{
    [Fact]
    public async Task Register_CreatesMissingResourceAndOperationOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContextSqlite>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContextSqlite(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var registry = new ResourceOperationRegistry(
            new TestDbContextFactory(options),
            new MemoryCache(new MemoryCacheOptions()));

        await registry.Register("Repo", "read");
        await registry.Register("Repo", "read");

        await using var assertContext = new AppDbContextSqlite(options);
        Assert.Equal(1, await assertContext.Resources.CountAsync(resource => resource.Name == "Repo", cancellationToken));
        Assert.Equal(1, await assertContext.Operations.CountAsync(operation => operation.Name == "read", cancellationToken));
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContextSqlite> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContextSqlite> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext() => new AppDbContextSqlite(_options);
    }
}
