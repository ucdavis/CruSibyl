using System.Text.Json;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Web.Models.Dashboard;
using CruSibyl.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CruSibyl.Tests;

public class DashboardServiceSummaryTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetDashboardOverviewAsync_PopulatesWebJobSummaryCountsFromConfiguredWindows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await DashboardTestDatabase.CreateAsync(cancellationToken);

        var app = new App { Name = "webjob-app", IsEnabled = true };
        var runningJob = new WebJob
        {
            App = app,
            Name = "running",
            JobType = "Continuous",
            Status = "Running",
            LastRunStatus = "Success",
            IsEnabled = true
        };
        var failedLastRunJob = new WebJob
        {
            App = app,
            Name = "failed-last-run",
            JobType = "Triggered",
            Status = "Stopped",
            LastRunStatus = "Failed",
            IsEnabled = true
        };
        var recentFailureJob = new WebJob
        {
            App = app,
            Name = "recent-failure",
            JobType = "Triggered",
            Status = "Stopped",
            LastRunStatus = "Success",
            IsEnabled = true
        };
        var consecutiveFailureJob = new WebJob
        {
            App = app,
            Name = "consecutive-failure",
            JobType = "Triggered",
            Status = "Stopped",
            LastRunStatus = "Success",
            IsEnabled = true
        };
        var disabledJob = new WebJob
        {
            App = app,
            Name = "disabled",
            JobType = "Triggered",
            Status = "Running",
            LastRunStatus = "Failed",
            IsEnabled = false
        };

        database.Context.AddRange(app, runningJob, failedLastRunJob, recentFailureJob, consecutiveFailureJob, disabledJob);
        AddWebJobStatusEvent(database.Context, recentFailureJob, FixedNow.AddDays(-2), new WebJobStatusPayload
        {
            StartTime = FixedNow.AddHours(-2).UtcDateTime,
            Status = "Failed"
        });

        AddWebJobStatusEvent(database.Context, consecutiveFailureJob, FixedNow.AddHours(-1), new WebJobStatusPayload
        {
            StartTime = FixedNow.AddHours(-1).UtcDateTime,
            Status = "Failed"
        });
        AddWebJobStatusEvent(database.Context, consecutiveFailureJob, FixedNow.AddHours(-2), new WebJobStatusPayload
        {
            StartTime = FixedNow.AddHours(-2).UtcDateTime,
            Status = "Failed"
        });
        AddWebJobStatusEvent(database.Context, consecutiveFailureJob, FixedNow.AddHours(-3), new WebJobStatusPayload
        {
            StartTime = FixedNow.AddHours(-3).UtcDateTime,
            Status = "Failed"
        });
        AddWebJobStatusEvent(database.Context, consecutiveFailureJob, FixedNow.AddHours(-4), new WebJobStatusPayload
        {
            StartTime = FixedNow.AddHours(-4).UtcDateTime,
            Status = "Success"
        }, severity: "Info");

        await database.Context.SaveChangesAsync(cancellationToken);

        var model = await database.CreateService().GetDashboardOverviewAsync();
        var runningDetails = await database.CreateService().GetWebJobDetailsAsync(WebJobDetailFilter.Running);
        var recentFailureDetails = await database.CreateService().GetWebJobDetailsAsync(WebJobDetailFilter.RecentlyFailed);
        var consecutiveFailureDetails = await database.CreateService().GetWebJobDetailsAsync(WebJobDetailFilter.ConsecutiveFailures);

        Assert.Equal(4, model.WebJobs.TotalEnabledJobs);
        Assert.Equal(1, model.WebJobs.RunningJobs);
        Assert.Equal(1, model.WebJobs.JobsWithFailedLastRun);
        Assert.Equal(2, model.WebJobs.JobsRecentlyFailed);
        Assert.Equal(1, model.WebJobs.JobsFailingConsecutiveAttempts);

        Assert.Equal("running", Assert.Single(runningDetails.Rows).WebJobName);
        Assert.Equal(["consecutive-failure", "recent-failure"], recentFailureDetails.Rows.Select(row => row.WebJobName).Order().ToArray());
        Assert.Equal("consecutive-failure", Assert.Single(consecutiveFailureDetails.Rows).WebJobName);
    }

    [Fact]
    public async Task GetFailureHistoryAsync_UsesPayloadRunTimestampBeforeEventTimestamp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await DashboardTestDatabase.CreateAsync(cancellationToken);

        var app = new App { Name = "history-app", IsEnabled = true };
        var webJob = new WebJob
        {
            App = app,
            Name = "history-job",
            JobType = "Triggered",
            Status = "Stopped",
            LastRunStatus = "Failed",
            IsEnabled = true
        };
        database.Context.AddRange(app, webJob);

        var payloadStart = FixedNow.AddHours(-3).UtcDateTime;
        AddWebJobStatusEvent(database.Context, webJob, FixedNow.AddDays(-2), new WebJobStatusPayload
        {
            StartTime = payloadStart,
            Status = "Failed",
            ErrorOutput = "payload-time failure"
        });

        await database.Context.SaveChangesAsync(cancellationToken);

        var model = await database.CreateService().GetFailureHistoryAsync();

        var failure = Assert.Single(model.Failures);
        Assert.Equal(payloadStart, failure.FailureTime);
        Assert.Equal("payload-time failure", failure.ErrorOutput);
    }

    [Fact]
    public async Task GetDashboardOverviewAsync_PopulatesAppMappingAndFreshnessCounts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await DashboardTestDatabase.CreateAsync(cancellationToken);

        var freshRepo = AddRepo(database.Context, "fresh", FixedNow.AddDays(-1), ScanStatus.Completed, withManifest: true);
        var manifestlessRepo = AddRepo(database.Context, "manifestless", FixedNow.AddDays(-1), ScanStatus.Completed, withManifest: false);
        var staleRepo = AddRepo(database.Context, "stale", FixedNow.AddDays(-9), ScanStatus.Completed, withManifest: true);
        var failedRepo = AddRepo(database.Context, "failed", FixedNow.AddDays(-1), ScanStatus.Failed, withManifest: true);

        database.Context.Apps.AddRange(
            new App { Name = "mapped-fresh", IsEnabled = true, Repo = freshRepo },
            new App { Name = "mapped-manifestless", IsEnabled = true, Repo = manifestlessRepo },
            new App { Name = "mapped-stale", IsEnabled = true, Repo = staleRepo },
            new App { Name = "mapped-failed", IsEnabled = true, Repo = failedRepo },
            new App { Name = "unmapped", IsEnabled = true },
            new App { Name = "disabled-unmapped", IsEnabled = false });

        AddPackage(database.Context, "fresh-package", FixedNow.AddDays(-1), ScanStatus.Completed);
        AddPackage(database.Context, "stale-package", FixedNow.AddDays(-9), ScanStatus.Completed);
        AddPackage(database.Context, "failed-package", FixedNow.AddDays(-1), ScanStatus.Failed);

        await database.Context.SaveChangesAsync(cancellationToken);

        var model = await database.CreateService().GetDashboardOverviewAsync();
        var unmappedDetails = await database.CreateService().GetAppMappingDetailsAsync(AppMappingDetailFilter.Unmapped);
        var staleRepoDetails = await database.CreateService().GetDataFreshnessDetailsAsync(DataFreshnessDetailFilter.StaleRepoScans);

        Assert.Equal(5, model.AppMappings.EnabledApps);
        Assert.Equal(4, model.AppMappings.EnabledAppsWithMappedRepos);
        Assert.Equal(1, model.AppMappings.UnmappedEnabledApps);
        Assert.Equal(1, model.AppMappings.MappedAppsWithManifestlessRepos);
        Assert.Equal(1, model.AppMappings.MappedAppsWithStaleRepoScans);
        Assert.Equal(1, model.AppMappings.MappedAppsWithFailedRepoScans);

        Assert.Equal(1, model.DataFreshness.StaleRepoScans);
        Assert.Equal(1, model.DataFreshness.FailedRepoScans);
        Assert.Equal(1, model.DataFreshness.StalePackageScans);
        Assert.Equal(1, model.DataFreshness.FailedPackageScans);
        Assert.Equal(1, model.DataFreshness.AppsWithoutRepos);
        Assert.Equal(1, model.DataFreshness.ReposWithoutManifests);

        var unmappedRow = Assert.Single(unmappedDetails.Rows);
        Assert.Equal("unmapped", unmappedRow.AppName);
        Assert.False(unmappedRow.RepoId.HasValue);

        var staleRepoRow = Assert.Single(staleRepoDetails.Rows);
        Assert.Equal("stale", staleRepoRow.Name);
        Assert.Equal("Repo", staleRepoRow.ItemType);
    }

    [Fact]
    public async Task GetDashboardOverviewAsync_PopulatesPlatformCurrencyCountsWithSemverAndUnknownHandling()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await DashboardTestDatabase.CreateAsync(cancellationToken);

        var platform = new Platform { Name = "dotnet" };
        var version7 = new PlatformVersion { Platform = platform, Version = "7.0.0" };
        var version8 = new PlatformVersion { Platform = platform, Version = "8.0.0" };
        var prereleaseOnlyPlatform = new Platform { Name = "preview-only" };
        var prereleaseOnlyVersion = new PlatformVersion { Platform = prereleaseOnlyPlatform, Version = "9.0.0-preview.1" };
        var unparsablePlatform = new Platform { Name = "unparsable" };
        var unparsableVersion = new PlatformVersion { Platform = unparsablePlatform, Version = "not-a-version" };

        var outdatedRepo = AddRepo(database.Context, "outdated-platform", FixedNow.AddDays(-1), ScanStatus.Completed);
        outdatedRepo.Manifests.Add(new Manifest { FilePath = "outdated.csproj", PlatformVersion = version7 });
        var currentRepo = AddRepo(database.Context, "current-platform", FixedNow.AddDays(-1), ScanStatus.Completed);
        currentRepo.Manifests.Add(new Manifest { FilePath = "current.csproj", PlatformVersion = version8 });
        var prereleaseOnlyRepo = AddRepo(database.Context, "preview-platform", FixedNow.AddDays(-1), ScanStatus.Completed);
        prereleaseOnlyRepo.Manifests.Add(new Manifest { FilePath = "preview.csproj", PlatformVersion = prereleaseOnlyVersion });
        var unparsableRepo = AddRepo(database.Context, "unknown-platform", FixedNow.AddDays(-1), ScanStatus.Completed);
        unparsableRepo.Manifests.Add(new Manifest { FilePath = "unknown.csproj", PlatformVersion = unparsableVersion });
        var failedRepo = AddRepo(database.Context, "failed-platform-scan", FixedNow.AddDays(-1), ScanStatus.Failed, withManifest: true);
        var staleRepo = AddRepo(database.Context, "stale-platform-scan", FixedNow.AddDays(-9), ScanStatus.Completed, withManifest: true);

        database.Context.PlatformVersions.AddRange(version7, version8, prereleaseOnlyVersion, unparsableVersion);
        database.Context.Apps.AddRange(
            new App { Name = "outdated-app", IsEnabled = true, Repo = outdatedRepo },
            new App { Name = "current-app", IsEnabled = true, Repo = currentRepo },
            new App { Name = "preview-app", IsEnabled = true, Repo = prereleaseOnlyRepo },
            new App { Name = "unknown-app", IsEnabled = true, Repo = unparsableRepo },
            new App { Name = "failed-scan-app", IsEnabled = true, Repo = failedRepo },
            new App { Name = "stale-scan-app", IsEnabled = true, Repo = staleRepo });

        await database.Context.SaveChangesAsync(cancellationToken);

        var model = await database.CreateService().GetDashboardOverviewAsync();
        var outdatedDetails = await database.CreateService().GetPlatformDetailsAsync(PlatformDetailFilter.Outdated);
        var unknownDetails = await database.CreateService().GetPlatformDetailsAsync(PlatformDetailFilter.Unknown);

        Assert.Equal(6, model.Platform.EnabledAppsWithManifests);
        Assert.Equal(1, model.Platform.AppsOnOutdatedManifestPlatformVersions);
        Assert.Equal(1, model.Platform.ReposOnOutdatedManifestPlatformVersions);
        Assert.Equal(2, model.Platform.UnknownPlatformVersionData);
        Assert.Equal(1, model.Platform.FailedManifestScans);
        Assert.Equal(1, model.Platform.StaleManifestScans);

        var outdatedRow = Assert.Single(outdatedDetails.Rows);
        Assert.Equal("outdated-app", outdatedRow.AppName);
        Assert.Equal("Outdated", outdatedRow.CurrencyStatus);
        Assert.Equal("8.0.0", outdatedRow.LatestStableVersion);

        Assert.Equal(2, unknownDetails.Rows.Count);
        Assert.All(unknownDetails.Rows, row => Assert.Equal("Unknown", row.CurrencyStatus));
    }

    [Fact]
    public async Task GetDashboardOverviewAsync_ClassifiesDependencyCurrencyFromNumericVersionParts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await DashboardTestDatabase.CreateAsync(cancellationToken);

        var platformVersion = new PlatformVersion { Platform = new Platform { Name = "npm-manifest" }, Version = "1.0.0" };
        var repo = AddRepo(database.Context, "dependency-repo", FixedNow.AddDays(-1), ScanStatus.Completed);
        var manifest = new Manifest { Repo = repo, FilePath = "package.json", PlatformVersion = platformVersion };
        repo.Manifests.Add(manifest);
        database.Context.Apps.Add(new App { Name = "dependency-app", IsEnabled = true, Importance = 0.8, Repo = repo });

        AddDependency(
            database.Context,
            manifest,
            "major-package",
            new PackageVersionSpec("1.0.0", 1, 0, 0, null),
            [new PackageVersionSpec("2.0.0", 2, 0, 0, null)]);
        AddDependency(
            database.Context,
            manifest,
            "minor-package",
            new PackageVersionSpec("1.1.0", 1, 1, 0, null),
            [new PackageVersionSpec("1.2.0", 1, 2, 0, null)]);
        AddDependency(
            database.Context,
            manifest,
            "patch-package",
            new PackageVersionSpec("1.1.1", 1, 1, 1, null),
            [new PackageVersionSpec("1.1.2", 1, 1, 2, null)]);
        AddDependency(
            database.Context,
            manifest,
            "unknown-package",
            new PackageVersionSpec("workspace:*", null, null, null, null),
            [new PackageVersionSpec("1.0.0", 1, 0, 0, null)]);
        AddDependency(
            database.Context,
            manifest,
            "current-package",
            new PackageVersionSpec("1.0.0", 1, 0, 0, null),
            []);

        await database.Context.SaveChangesAsync(cancellationToken);

        var model = await database.CreateService().GetDashboardOverviewAsync();
        var majorDetails = await database.CreateService().GetDependencyDetailsAsync(DependencyDetailFilter.Major);
        var highPriorityDetails = await database.CreateService().GetDependencyDetailsAsync(DependencyDetailFilter.HighPriority);
        var unknownDetails = await database.CreateService().GetDependencyDetailsAsync(DependencyDetailFilter.Unknown);

        Assert.Equal(5, model.Dependency.TotalTrackedDependencies);
        Assert.Equal(3, model.Dependency.OutdatedDependencies);
        Assert.Equal(1, model.Dependency.MajorBehindDependencies);
        Assert.Equal(1, model.Dependency.MinorBehindDependencies);
        Assert.Equal(1, model.Dependency.PatchBehindDependencies);
        Assert.Equal(1, model.Dependency.UnknownCurrencyDependencies);
        Assert.Equal(2, model.Dependency.HighPriorityOutdatedDependencies);
        Assert.Equal(1, model.Dependency.EnabledAppsWithDependencyCoverage);
        Assert.Equal(0, model.Dependency.EnabledAppsWithoutDependencyCoverage);

        var majorRow = Assert.Single(majorDetails.Dependencies);
        Assert.Equal("major-package", majorRow.PackageName);
        Assert.Equal("High", majorRow.SeverityLevel);
        Assert.Equal(2, highPriorityDetails.Dependencies.Count);
        Assert.Equal("unknown-package", Assert.Single(unknownDetails.Dependencies).PackageName);
    }

    private static void AddWebJobStatusEvent(
        AppDbContext dbContext,
        WebJob webJob,
        DateTimeOffset eventTimestamp,
        WebJobStatusPayload payload,
        string severity = "Error")
    {
        var evt = new Event
        {
            EventType = EventType.WebJobStatus,
            Timestamp = eventTimestamp.UtcDateTime,
            Severity = severity,
            Message = payload.Status ?? "WebJob status",
            Payload = JsonSerializer.SerializeToElement(payload)
        };
        evt.WebJobs.Add(webJob);
        dbContext.Events.Add(evt);
    }

    private static Repo AddRepo(
        AppDbContext dbContext,
        string name,
        DateTimeOffset lastScannedAt,
        ScanStatus scanStatus,
        bool withManifest = false)
    {
        var repo = new Repo
        {
            Name = name,
            LastScannedAt = lastScannedAt.UtcDateTime,
            ScanStatus = scanStatus
        };

        if (withManifest)
        {
            repo.Manifests.Add(new Manifest
            {
                FilePath = $"{name}.manifest",
                PlatformVersion = new PlatformVersion
                {
                    Platform = new Platform { Name = $"{name}-platform" },
                    Version = "1.0.0"
                }
            });
        }

        dbContext.Repos.Add(repo);
        return repo;
    }

    private static void AddPackage(
        AppDbContext dbContext,
        string name,
        DateTimeOffset lastScannedAt,
        ScanStatus scanStatus)
    {
        dbContext.Packages.Add(new Package
        {
            Name = name,
            Platform = new Platform { Name = $"{name}-platform" },
            LastScannedAt = lastScannedAt.UtcDateTime,
            ScanStatus = scanStatus
        });
    }

    private static void AddDependency(
        AppDbContext dbContext,
        Manifest manifest,
        string packageName,
        PackageVersionSpec current,
        IReadOnlyCollection<PackageVersionSpec> laterVersions)
    {
        var package = new Package
        {
            Name = packageName,
            Platform = new Platform { Name = $"npm-{packageName}" },
            LastScannedAt = FixedNow.AddDays(-1).UtcDateTime,
            ScanStatus = ScanStatus.Completed
        };
        var currentVersion = CreatePackageVersion(package, current);

        package.Versions.Add(currentVersion);
        foreach (var laterVersion in laterVersions)
        {
            package.Versions.Add(CreatePackageVersion(package, laterVersion));
        }

        dbContext.Dependencies.Add(new Dependency
        {
            Manifest = manifest,
            PackageVersion = currentVersion
        });
    }

    private static PackageVersion CreatePackageVersion(Package package, PackageVersionSpec spec)
        => new()
        {
            Package = package,
            Version = spec.Version,
            Major = spec.Major,
            Minor = spec.Minor,
            Patch = spec.Patch,
            PreRelease = spec.PreRelease
        };

    private sealed record PackageVersionSpec(string Version, int? Major, int? Minor, int? Patch, string? PreRelease);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }

    private sealed class DashboardTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DashboardTestDatabase(SqliteConnection connection, AppDbContextSqlite context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContextSqlite Context { get; }

        public static async Task<DashboardTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            var options = new DbContextOptionsBuilder<AppDbContextSqlite>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContextSqlite(options);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            return new DashboardTestDatabase(connection, context);
        }

        public DashboardService CreateService()
            => new(
                Context,
                Options.Create(new DashboardSettings
                {
                    ConsecutiveFailureAttempts = 3,
                    RecentFailureWindowHours = 24,
                    HistoryWindowDays = 30,
                    StaleScanDays = 7
                }),
                new FixedTimeProvider());

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
