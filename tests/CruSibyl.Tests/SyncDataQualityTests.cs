using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Core.Services;
using CruSibyl.Web.Models.Dashboard;
using CruSibyl.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CruSibyl.Tests;

public class SyncDataQualityTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SyncWebJobStatuses_UsesSeparateDbContextPerConcurrentWebJob()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await SyncTestDatabase.CreateAsync(cancellationToken);

        var app = new App { Name = "sync-app", SubscriptionId = "sub-id", IsEnabled = true };
        database.Context.WebJobs.AddRange(
            new WebJob { App = app, Name = "first-job", JobType = "Triggered", IsEnabled = true },
            new WebJob { App = app, Name = "second-job", JobType = "Triggered", IsEnabled = true });
        await database.Context.SaveChangesAsync(cancellationToken);

        var azureQueryService = new EmptyRunHistoryAzureQueryService();
        var service = new AzureSyncService(
            database.ContextFactory,
            azureQueryService,
            new EventService(database.ContextFactory),
            Options.Create(new AzureSettings
            {
                SubscriptionId = "sub-id",
                MaxConcurrentStatusQueries = 2
            }));

        await service.SyncWebJobStatuses(cancellationToken);

        Assert.Equal(2, azureQueryService.RunHistoryCallCount);
        Assert.Equal(3, database.ContextFactory.CreatedContextCount);
    }

    [Fact]
    public async Task SyncManifestsAsync_StampsRepoScanMetadataPerRepo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await SyncTestDatabase.CreateAsync(cancellationToken);

        database.Context.Repos.AddRange(
            new Repo { Name = "healthy-repo" },
            new Repo { Name = "broken-repo" });
        await database.Context.SaveChangesAsync(cancellationToken);

        var service = new ManifestSyncService(new ManifestSyncGitHubService(), database.Context);

        var result = await service.SyncManifestsAsync(timeLimitInMinutes: 9);

        Assert.True(result.IsError);

        var healthyRepo = await database.Context.Repos
            .Include(repo => repo.Manifests)
            .SingleAsync(repo => repo.Name == "healthy-repo", cancellationToken);
        var brokenRepo = await database.Context.Repos
            .SingleAsync(repo => repo.Name == "broken-repo", cancellationToken);

        Assert.Equal(ScanStatus.Completed, healthyRepo.ScanStatus);
        Assert.NotNull(healthyRepo.LastScannedAt);
        Assert.Null(healthyRepo.ScanMessage);
        Assert.Single(healthyRepo.Manifests);

        Assert.Equal(ScanStatus.Failed, brokenRepo.ScanStatus);
        Assert.NotNull(brokenRepo.LastScannedAt);
        Assert.Contains("GitHub search failed", brokenRepo.ScanMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackageRegistryFailures_AreReportedThroughPackageScanMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await SyncTestDatabase.CreateAsync(cancellationToken);

        var node = new Platform { Name = "node" };
        database.Context.Packages.Add(new Package { Name = "broken-package", Platform = node });
        await database.Context.SaveChangesAsync(cancellationToken);

        var syncService = new PackageVersionSyncService(
            new EmptyNuGetService(),
            new FailingNpmService(),
            database.ContextFactory);

        var result = await syncService.SyncPackageVersionsAsync();

        Assert.False(result.IsError);

        database.Context.ChangeTracker.Clear();
        var package = await database.Context.Packages.SingleAsync(cancellationToken);
        Assert.Equal(ScanStatus.Failed, package.ScanStatus);
        Assert.NotNull(package.LastScannedAt);
        Assert.Contains("registry unavailable", package.ScanMessage, StringComparison.Ordinal);

        var dashboardService = new DashboardService(
            database.Context,
            Options.Create(new DashboardSettings
            {
                ConsecutiveFailureAttempts = 3,
                RecentFailureWindowHours = 24,
                HistoryWindowDays = 30,
                StaleScanDays = 7
            }),
            new FixedTimeProvider());

        var overview = await dashboardService.GetDashboardOverviewAsync();
        var details = await dashboardService.GetDataFreshnessDetailsAsync(DataFreshnessDetailFilter.FailedPackageScans);

        Assert.Equal(1, overview.DataFreshness.FailedPackageScans);
        var row = Assert.Single(details.Rows);
        Assert.Equal("Package", row.ItemType);
        Assert.Equal("broken-package", row.Name);
        Assert.Equal(ScanStatus.Failed, row.ScanStatus);
        Assert.NotNull(row.LastScannedAt);
        Assert.Contains("registry unavailable", row.ScanMessage, StringComparison.Ordinal);
    }

    private sealed class EmptyRunHistoryAzureQueryService : IAzureQueryService
    {
        public int RunHistoryCallCount { get; private set; }

        public Task<List<App>> GetApps(CancellationToken cancellationToken = default) => Task.FromResult(new List<App>());

        public Task<List<WebJob>> GetWebJobs(App app, CancellationToken cancellationToken = default) => Task.FromResult(new List<WebJob>());

        public Task<WebJobStatus?> GetWebJobStatus(WebJob webJob, CancellationToken cancellationToken = default) => Task.FromResult<WebJobStatus?>(null);

        public Task<KuduTriggeredWebJobDetails?> GetWebJobRunHistory(WebJob webJob, CancellationToken cancellationToken = default)
        {
            RunHistoryCallCount++;
            return Task.FromResult<KuduTriggeredWebJobDetails?>(new KuduTriggeredWebJobDetails
            {
                LatestRuns = []
            });
        }

        public Task<string?> GetWebJobRunOutput(string outputUrl, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class ManifestSyncGitHubService : IGitHubService
    {
        public Task<List<ManifestData>> GetManifests(string repo)
        {
            if (repo == "broken-repo")
            {
                throw new InvalidOperationException("GitHub search failed");
            }

            return Task.FromResult(new List<ManifestData>
            {
                new()
                {
                    Platform = "dotnet",
                    PlatformVersion = "8.0.0",
                    Path = "src/app.csproj"
                }
            });
        }
    }

    private sealed class EmptyNuGetService : INuGetService
    {
        public Task<List<PackageVersionInfo>> GetLatestMinorReleasesByMajorAsync(
            string packageName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PackageVersionInfo>());
    }

    private sealed class FailingNpmService : INpmService
    {
        public Task<List<PackageVersionInfo>> GetLatestMinorReleasesByMajorAsync(
            string packageName,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("registry unavailable");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }

    private sealed class CountingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContextSqlite> _options;
        private int _createdContextCount;

        public CountingDbContextFactory(DbContextOptions<AppDbContextSqlite> options)
        {
            _options = options;
        }

        public int CreatedContextCount => _createdContextCount;

        public AppDbContext CreateDbContext()
        {
            Interlocked.Increment(ref _createdContextCount);
            return new AppDbContextSqlite(_options);
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _createdContextCount);
            return ValueTask.FromResult<AppDbContext>(new AppDbContextSqlite(_options));
        }
    }

    private sealed class SyncTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SyncTestDatabase(
            SqliteConnection connection,
            AppDbContextSqlite context,
            CountingDbContextFactory contextFactory)
        {
            _connection = connection;
            Context = context;
            ContextFactory = contextFactory;
        }

        public AppDbContextSqlite Context { get; }

        public CountingDbContextFactory ContextFactory { get; }

        public static async Task<SyncTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var connectionString = $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var options = new DbContextOptionsBuilder<AppDbContextSqlite>()
                .UseSqlite(connectionString)
                .Options;
            var context = new AppDbContextSqlite(options);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            return new SyncTestDatabase(connection, context, new CountingDbContextFactory(options));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
