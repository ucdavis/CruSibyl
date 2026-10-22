using Htmx;
using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.Models.Builders;
using Htmx.Components.NavBar;
using Htmx.Components.Services;
using Htmx.Components.Table;
using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Mvc;
using CruSibyl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using CruSibyl.Web.Models.Dashboard;
using CruSibyl.Core.Data;
using Microsoft.EntityFrameworkCore;
using Result = Htmx.Components.Models.Result;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly AppDbContext _dbContext;
    private readonly IModelHandlerFactoryGeneric _modelHandlerFactory;
    private readonly ITableProvider _tableProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DashboardController(
        IDashboardService dashboardService,
        AppDbContext dbContext,
        IModelHandlerFactoryGeneric modelHandlerFactory,
        ITableProvider tableProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _dashboardService = dashboardService;
        _dbContext = dbContext;
        _modelHandlerFactory = modelHandlerFactory;
        _tableProvider = tableProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    [NavAction(DisplayName = "Dashboard", Icon = "fas fa-tachometer-alt", Order = 0, PushUrl = true, ViewName = "_Content")]
    public async Task<IActionResult> Index()
    {
        var model = await _dashboardService.GetDashboardOverviewAsync();
        return Ok(model);
    }

    [HttpGet("/Dashboard/FailureHistory")]
    public async Task<IActionResult> FailureHistory(string? app = null, string? job = null)
    {
        var model = await _dashboardService.GetFailureHistoryAsync(app, job);
        model.Table = await BuildTableModelAsync<WebJobFailureHistoryModel, string>(
            DashboardTableTypeIds.FailureHistory,
            TableComponentIds.DashboardFailureHistory(app, job),
            model.Failures);

        return PartialView("_FailureHistory", model);
    }

    [HttpGet("/Dashboard/Details/WebJobs")]
    public async Task<IActionResult> WebJobDetails(WebJobDetailFilter filter = WebJobDetailFilter.TotalEnabled)
    {
        var model = await _dashboardService.GetWebJobDetailsAsync(filter);
        var table = await BuildTableModelAsync<WebJobDetailRowModel, int>(
            DashboardTableTypeIds.WebJobDetails,
            TableComponentIds.DashboardWebJobDetails(filter),
            model.Rows);

        return PartialView("_DetailTable", new DashboardDetailTableViewModel
        {
            Title = model.Title,
            EmptyMessage = "No WebJobs match this filter.",
            Table = table
        });
    }

    [HttpGet("/Dashboard/Details/Platforms")]
    public async Task<IActionResult> PlatformDetails(PlatformDetailFilter filter = PlatformDetailFilter.WithManifests)
    {
        var model = await _dashboardService.GetPlatformDetailsAsync(filter);
        var table = await BuildTableModelAsync<PlatformDetailRowModel, string>(
            DashboardTableTypeIds.PlatformDetails,
            TableComponentIds.DashboardPlatformDetails(filter),
            model.Rows);

        return PartialView("_DetailTable", new DashboardDetailTableViewModel
        {
            Title = model.Title,
            EmptyMessage = "No platform rows match this filter.",
            Table = table
        });
    }

    [HttpGet("/Dashboard/Details/Dependencies")]
    public async Task<IActionResult> DependencyDetails(DependencyDetailFilter filter = DependencyDetailFilter.All)
    {
        var model = await _dashboardService.GetDependencyDetailsAsync(filter);
        var table = await BuildTableModelAsync<DependencyCurrencyModel, string>(
            DashboardTableTypeIds.DependencyDetails,
            TableComponentIds.DashboardDependencyDetails(filter),
            model.Dependencies);

        return PartialView("_DetailTable", new DashboardDetailTableViewModel
        {
            Title = "Dependency & Platform Currency",
            EmptyMessage = "No dependency data is available for this filter.",
            Table = table
        });
    }

    [HttpGet("/Dashboard/Details/AppMappings")]
    public async Task<IActionResult> AppMappingDetails(AppMappingDetailFilter filter = AppMappingDetailFilter.Unmapped)
    {
        var model = await _dashboardService.GetAppMappingDetailsAsync(filter);
        var table = await BuildTableModelAsync<AppMappingDetailRowModel, int>(
            DashboardTableTypeIds.AppMappingDetails,
            TableComponentIds.DashboardAppMappingDetails(filter),
            model.Rows);

        return PartialView("_DetailTable", new DashboardDetailTableViewModel
        {
            Title = model.Title,
            EmptyMessage = "No app mappings match this filter.",
            Table = table
        });
    }

    [HttpGet("/Dashboard/Details/Freshness")]
    public async Task<IActionResult> DataFreshnessDetails(DataFreshnessDetailFilter filter = DataFreshnessDetailFilter.StaleRepoScans)
    {
        var model = await _dashboardService.GetDataFreshnessDetailsAsync(filter);
        var table = await BuildTableModelAsync<DataFreshnessDetailRowModel, string>(
            DashboardTableTypeIds.DataFreshnessDetails,
            TableComponentIds.DashboardDataFreshnessDetails(filter),
            model.Rows);

        return PartialView("_DetailTable", new DashboardDetailTableViewModel
        {
            Title = model.Title,
            EmptyMessage = "No freshness rows match this filter.",
            Table = table
        });
    }

    [HttpGet("/Dashboard/WebJobDrillDown/{webJobId}")]
    public async Task<IActionResult> WebJobDrillDown(int webJobId)
    {
        var model = await _dashboardService.GetWebJobDrillDownAsync(webJobId);
        return PartialView("_WebJobDrillDown", model);
    }

    [HttpGet("/Dashboard/DependencyCurrency")]
    public async Task<IActionResult> DependencyCurrency()
    {
        var model = await _dashboardService.GetDependencyCurrencyAsync();
        var table = await BuildTableModelAsync<DependencyCurrencyModel, string>(
            DashboardTableTypeIds.DependencyDetails,
            TableComponentIds.DashboardDependencyDetails(DependencyDetailFilter.All),
            model.Dependencies);

        return PartialView("_DetailTable", new DashboardDetailTableViewModel
        {
            Title = "Dependency & Platform Currency",
            EmptyMessage = "No dependency data available.",
            Table = table
        });
    }

    private async Task<TableModel<T, TKey>> BuildTableModelAsync<T, TKey>(
        string typeId,
        string componentId,
        IEnumerable<T> rows)
        where T : class, new()
    {
        var modelHandler = await _modelHandlerFactory.Get<T, TKey>(typeId, ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModelAsync();
        tableModel.ComponentId = componentId;
        await _tableProvider.FetchPageAsync(tableModel, rows.AsQueryable(), new TableState());

        return tableModel;
    }

    [ModelConfig(DashboardTableTypeIds.WebJobDetails)]
    private void ConfigureWebJobDetail(ModelHandlerBuilder<WebJobDetailRowModel, int> builder)
    {
        builder
            .WithKeySelector(row => row.WebJobId)
            .WithRowsAsync(GetWebJobDetailRowsForRequestAsync)
            .WithTable(table => table
                .AddSelectorColumn(row => row.AppName, column => column.WithHeader("App"))
                .AddSelectorColumn(row => row.WebJobName, column => column.WithHeader("WebJob"))
                .AddSelectorColumn(row => row.JobType, column => column.WithHeader("Type"))
                .AddSelectorColumn(row => row.Status!, column => column.WithCellPartial(DashboardCellPartials.StatusBadge))
                .AddSelectorColumn(row => row.LastRunStatus!, column => column.WithHeader("Last Run").WithCellPartial(DashboardCellPartials.WebJobLastRun))
                .AddSelectorColumn(row => row.RunMode!, column => column.WithHeader("Run Mode"))
                .AddSelectorColumn(row => row.Schedule!)
                .AddDisplayColumn("Actions", column => column.WithActions((row, actions) =>
                {
                    var detailRow = row.Item;
                    actions.AddAction(action => action
                        .WithLabel("Details")
                        .WithIcon("fas fa-list")
                        .WithClass("btn-ghost")
                        .WithHxGet($"/Dashboard/WebJobDrillDown/{detailRow.WebJobId}")
                        .WithHxTarget("#dashboard-detail-modal-body")
                        .WithHxSwap("innerHTML")
                        .WithAttribute("data-hc-open-modal", "dashboard-detail-modal"));
                })));
    }

    [ModelConfig(DashboardTableTypeIds.FailureHistory)]
    private void ConfigureFailureHistory(ModelHandlerBuilder<WebJobFailureHistoryModel, string> builder)
    {
        builder
            .WithKeySelector(row => row.RowKey)
            .WithRowsAsync(GetFailureHistoryRowsForRequestAsync)
            .WithTable(table => table
                .AddSelectorColumn(row => row.AppName, column => column.WithHeader("App"))
                .AddSelectorColumn(row => row.WebJobName, column => column.WithHeader("WebJob"))
                .AddSelectorColumn(row => row.FailureTime, column => column.WithHeader("Time").WithCellPartial(DashboardCellPartials.DateTime))
                .AddSelectorColumn(row => row.Status, column => column.WithCellPartial(DashboardCellPartials.StatusBadge))
                .AddSelectorColumn(row => row.DurationMs!, column => column.WithHeader("Duration").WithCellPartial(DashboardCellPartials.Duration))
                .AddSelectorColumn(row => row.IsConsistent, column => column.WithHeader("Pattern").WithCellPartial(DashboardCellPartials.FailurePattern))
                .AddDisplayColumn("Trend", column => column.WithCellPartial(DashboardCellPartials.Sparkline))
                .AddDisplayColumn("Actions", column => column.WithActions((row, actions) =>
                {
                    var detailRow = row.Item;
                    actions.AddAction(action => action
                        .WithLabel("Details")
                        .WithIcon("fas fa-list")
                        .WithClass("btn-ghost")
                        .WithHxGet($"/Dashboard/WebJobDrillDown/{detailRow.WebJobId}")
                        .WithHxTarget("#dashboard-detail-modal-body")
                        .WithHxSwap("innerHTML")
                        .WithAttribute("data-hc-open-modal", "dashboard-detail-modal"));
                })));
    }

    [ModelConfig(DashboardTableTypeIds.PlatformDetails)]
    private void ConfigurePlatformDetail(ModelHandlerBuilder<PlatformDetailRowModel, string> builder)
    {
        builder
            .WithKeySelector(row => row.RowKey)
            .WithRowsAsync(GetPlatformDetailRowsForRequestAsync)
            .WithTable(table => table
                .AddSelectorColumn(row => row.AppName, column => column.WithHeader("App"))
                .AddSelectorColumn(row => row.RepoName!, column => column.WithHeader("Repo"))
                .AddSelectorColumn(row => row.ManifestPath!, column => column.WithHeader("Manifest").WithCellPartial(DashboardCellPartials.MonoText))
                .AddSelectorColumn(row => row.PlatformName!, column => column.WithHeader("Platform").WithCellPartial(DashboardCellPartials.OutlineBadge))
                .AddSelectorColumn(row => row.CurrentVersion!, column => column.WithHeader("Current").WithCellPartial(DashboardCellPartials.VersionDelta))
                .AddSelectorColumn(row => row.LatestStableVersion!, column => column.WithHeader("Latest Stable").WithCellPartial(DashboardCellPartials.VersionDelta))
                .AddSelectorColumn(row => row.CurrencyStatus, column => column.WithHeader("Currency").WithCellPartial(DashboardCellPartials.StatusBadge))
                .AddDisplayColumn("Scan", column => column.WithCellPartial(DashboardCellPartials.ScanStatus)));
    }

    [ModelConfig(DashboardTableTypeIds.DependencyDetails)]
    private void ConfigureDependencyDetail(ModelHandlerBuilder<DependencyCurrencyModel, string> builder)
    {
        builder
            .WithKeySelector(row => row.RowKey)
            .WithRowsAsync(GetDependencyRowsForRequestAsync)
            .WithTable(table => table
                .AddSelectorColumn(row => row.PriorityScore, column => column.WithHeader("Priority").WithCellPartial(DashboardCellPartials.Priority))
                .AddSelectorColumn(row => row.AppName, column => column.WithHeader("App"))
                .AddSelectorColumn(row => row.AppImportance, column => column.WithHeader("Importance").WithCellPartial(DashboardCellPartials.Importance))
                .AddSelectorColumn(row => row.PlatformName, column => column.WithHeader("Platform").WithCellPartial(DashboardCellPartials.OutlineBadge))
                .AddSelectorColumn(row => row.PackageName, column => column.WithHeader("Package").WithCellPartial(DashboardCellPartials.MonoText))
                .AddSelectorColumn(row => row.CurrentVersion, column => column.WithHeader("Current").WithCellPartial(DashboardCellPartials.VersionDelta))
                .AddSelectorColumn(row => row.LatestMajorVersion!, column => column.WithHeader("Latest Major").WithCellPartial(DashboardCellPartials.VersionDelta))
                .AddSelectorColumn(row => row.LatestMinorVersion!, column => column.WithHeader("Latest Minor").WithCellPartial(DashboardCellPartials.VersionDelta))
                .AddSelectorColumn(row => row.SeverityLevel, column => column.WithHeader("Severity").WithCellPartial(DashboardCellPartials.StatusBadge)));
    }

    [ModelConfig(DashboardTableTypeIds.AppMappingDetails)]
    private void ConfigureAppMappingDetail(ModelHandlerBuilder<AppMappingDetailRowModel, int> builder)
    {
        builder
            .WithKeySelector(row => row.AppId)
            .WithRowsAsync(GetAppMappingRowsForRequestAsync)
            .WithUpdate(UpdateAppMappingAsync)
            .WithInput(row => row.RepoId, input => input
                .WithKind(InputKind.Select)
                .WithLabel("Repo")
                .WithOptions(GetRepoOptions()))
            .WithTable(table => table
                .AddSelectorColumn(row => row.AppName, column => column.WithHeader("App"))
                .AddSelectorColumn(row => row.ResourceGroup!, column => column.WithHeader("Resource Group"))
                .AddSelectorColumn(row => row.RuntimeStack!, column => column.WithHeader("Runtime"))
                .AddSelectorColumn(row => row.RepoId!, column => column.WithHeader("Repo").WithEditable().WithCellPartial(DashboardCellPartials.RepoMapping))
                .AddSelectorColumn(row => row.ManifestCount, column => column.WithHeader("Manifests"))
                .AddDisplayColumn("Repo Scan", column => column.WithCellPartial(DashboardCellPartials.ScanStatus))
                .AddDisplayColumn("Candidate Repos", column => column.WithCellPartial(DashboardCellPartials.CandidateRepos))
                .AddCrudDisplayColumn());
    }

    [ModelConfig(DashboardTableTypeIds.DataFreshnessDetails)]
    private void ConfigureDataFreshnessDetail(ModelHandlerBuilder<DataFreshnessDetailRowModel, string> builder)
    {
        builder
            .WithKeySelector(row => row.RowKey)
            .WithRowsAsync(GetDataFreshnessRowsForRequestAsync)
            .WithTable(table => table
                .AddSelectorColumn(row => row.ItemType, column => column.WithHeader("Type").WithCellPartial(DashboardCellPartials.OutlineBadge))
                .AddSelectorColumn(row => row.Name)
                .AddSelectorColumn(row => row.RelatedName!, column => column.WithHeader("Related"))
                .AddSelectorColumn(row => row.LastScannedAt!, column => column.WithHeader("Last Scanned").WithCellPartial(DashboardCellPartials.DateTime))
                .AddSelectorColumn(row => row.ScanStatus!, column => column.WithHeader("Status").WithCellPartial(DashboardCellPartials.StatusBadge))
                .AddSelectorColumn(row => row.ScanMessage!, column => column.WithHeader("Message"))
                .AddSelectorColumn(row => row.Detail!));
    }

    private async Task<IReadOnlyList<WebJobDetailRowModel>> GetWebJobDetailRowsForRequestAsync()
        => (await _dashboardService.GetWebJobDetailsAsync(ResolveFilter(WebJobDetailFilter.TotalEnabled))).Rows;

    private async Task<IReadOnlyList<WebJobFailureHistoryModel>> GetFailureHistoryRowsForRequestAsync()
    {
        var (appFilter, jobFilter) = ResolveFailureHistoryFilters();
        return (await _dashboardService.GetFailureHistoryAsync(appFilter, jobFilter)).Failures;
    }

    private async Task<IReadOnlyList<PlatformDetailRowModel>> GetPlatformDetailRowsForRequestAsync()
        => (await _dashboardService.GetPlatformDetailsAsync(ResolveFilter(PlatformDetailFilter.WithManifests))).Rows;

    private async Task<IReadOnlyList<DependencyCurrencyModel>> GetDependencyRowsForRequestAsync()
        => (await _dashboardService.GetDependencyDetailsAsync(ResolveFilter(DependencyDetailFilter.All))).Dependencies;

    private async Task<IReadOnlyList<AppMappingDetailRowModel>> GetAppMappingRowsForRequestAsync()
        => (await _dashboardService.GetAppMappingDetailsAsync(ResolveFilter(AppMappingDetailFilter.Unmapped))).Rows;

    private async Task<IReadOnlyList<DataFreshnessDetailRowModel>> GetDataFreshnessRowsForRequestAsync()
        => (await _dashboardService.GetDataFreshnessDetailsAsync(ResolveFilter(DataFreshnessDetailFilter.StaleRepoScans))).Rows;

    private async Task<Result<AppMappingDetailRowModel>> UpdateAppMappingAsync(AppMappingDetailRowModel row)
    {
        var app = await _dbContext.Apps.FindAsync(row.AppId);
        if (app == null)
        {
            return Result.Error("App not found.");
        }

        if (row.RepoId.HasValue && !await _dbContext.Repos.AnyAsync(repo => repo.Id == row.RepoId.Value))
        {
            return Result.Error("Selected repo not found.");
        }

        app.RepoId = row.RepoId;
        await _dbContext.SaveChangesAsync();

        var updatedRow = await BuildAppMappingRowAsync(app.Id);
        return updatedRow == null
            ? Result.Error("Updated app mapping could not be loaded.")
            : Result.Value(updatedRow);
    }

    private async Task<AppMappingDetailRowModel?> BuildAppMappingRowAsync(int appId)
        => await _dbContext.Apps
            .Where(app => app.Id == appId)
            .Select(app => new AppMappingDetailRowModel
            {
                AppId = app.Id,
                AppName = app.Name,
                ResourceGroup = app.ResourceGroup,
                RuntimeStack = app.RuntimeStack,
                RepoId = app.RepoId,
                RepoName = app.Repo == null ? null : app.Repo.Name,
                RepoLastScannedAt = app.Repo == null ? null : app.Repo.LastScannedAt,
                RepoScanStatus = app.Repo == null ? null : app.Repo.ScanStatus,
                ManifestCount = app.Repo == null ? 0 : app.Repo.Manifests.Count
            })
            .SingleOrDefaultAsync();

    private IEnumerable<KeyValuePair<string, string>> GetRepoOptions()
    {
        yield return new KeyValuePair<string, string>("Unmapped", "");

        foreach (var repo in _dbContext.Repos.AsNoTracking().OrderBy(repo => repo.Name).Select(repo => new { repo.Id, repo.Name }))
        {
            yield return new KeyValuePair<string, string>(repo.Name, repo.Id.ToString());
        }
    }

    private TFilter ResolveFilter<TFilter>(TFilter fallback)
        where TFilter : struct, Enum
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return fallback;
        }

        if (request.Query.TryGetValue("filter", out var queryFilter)
            && Enum.TryParse<TFilter>(queryFilter.ToString(), ignoreCase: true, out var parsedQueryFilter))
        {
            return parsedQueryFilter;
        }

        var componentId = GetRequestValue("componentId");
        if (!string.IsNullOrWhiteSpace(componentId))
        {
            foreach (var value in Enum.GetValues<TFilter>())
            {
                var normalized = value.ToString().ToLowerInvariant();
                if (componentId.EndsWith($"-{normalized}", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
        }

        return fallback;
    }

    private string? GetRequestValue(string key)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return null;
        }

        if (request.Query.TryGetValue(key, out var queryValue))
        {
            return queryValue.ToString();
        }

        if (request.HasFormContentType && request.Form.TryGetValue(key, out var formValue))
        {
            return formValue.ToString();
        }

        return null;
    }

    private (string? AppFilter, string? JobFilter) ResolveFailureHistoryFilters()
    {
        var appFilter = NormalizeTextFilter(GetRequestValue("app"));
        var jobFilter = NormalizeTextFilter(GetRequestValue("job"));

        if (appFilter != null || jobFilter != null)
        {
            return (appFilter, jobFilter);
        }

        return TableComponentIds.TryParseDashboardFailureHistoryFilters(GetRequestValue("componentId"), out appFilter, out jobFilter)
            ? (appFilter, jobFilter)
            : (null, null);
    }

    private static string? NormalizeTextFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class DashboardCellPartials
{
    public const string StatusBadge = "~/Views/Dashboard/Cells/_StatusBadge.cshtml";
    public const string WebJobLastRun = "~/Views/Dashboard/Cells/_WebJobLastRun.cshtml";
    public const string MonoText = "~/Views/Dashboard/Cells/_MonoText.cshtml";
    public const string OutlineBadge = "~/Views/Dashboard/Cells/_OutlineBadge.cshtml";
    public const string VersionDelta = "~/Views/Dashboard/Cells/_VersionDelta.cshtml";
    public const string ScanStatus = "~/Views/Dashboard/Cells/_ScanStatus.cshtml";
    public const string Duration = "~/Views/Dashboard/Cells/_Duration.cshtml";
    public const string FailurePattern = "~/Views/Dashboard/Cells/_FailurePattern.cshtml";
    public const string Sparkline = "~/Views/Dashboard/Cells/_Sparkline.cshtml";
    public const string Priority = "~/Views/Dashboard/Cells/_Priority.cshtml";
    public const string Importance = "~/Views/Dashboard/Cells/_Importance.cshtml";
    public const string RepoMapping = "~/Views/Dashboard/Cells/_RepoMapping.cshtml";
    public const string CandidateRepos = "~/Views/Dashboard/Cells/_CandidateRepos.cshtml";
    public const string DateTime = "~/Views/Dashboard/Cells/_DateTime.cshtml";
}
