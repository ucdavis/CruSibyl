using CruSibyl.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Htmx.Components;
using Htmx.Components.Models;
using Htmx.Components.Services;
using Htmx.Components.Models.Table;
using Htmx.Components.Attributes;
using Htmx.Components.Models.Builders;
using CruSibyl.Web.Models.Reports;

namespace CruSibyl.Web.Controllers;

[Authorize]
[Route("Reports")]
[NavActionGroup(DisplayName = "Reports", Icon = "fas fa-chart-bar", Order = 1)]
public class ReportsController : TabController
{
    private readonly AppDbContext _dbContext;
    private readonly IModelHandlerFactoryGeneric _modelHandlerFactory;


    public ReportsController(AppDbContext dbContext, IModelHandlerFactoryGeneric modelHandlerFactory)
    {
        _dbContext = dbContext;
        _modelHandlerFactory = modelHandlerFactory;
    }

    [HttpGet("PackageVersions")]
    [NavAction(Icon = "fas fa-database", Order = 1, PushUrl = true, ViewName = "_PackageVersions")]
    public async Task<IActionResult> PackageVersions()
    {
        var pageState = this.GetPageState();
        var tableState = new TableState();
        pageState.Set("Table", "State", tableState);

        var modelHandler = await _modelHandlerFactory.Get<DependencyVersionReportModel, NoKey>(nameof(DependencyVersionReportModel), ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModelAndFetchPage(tableState);

        return Ok(tableModel);
    }

    public class NoKey { }


    [ModelConfig(nameof(DependencyVersionReportModel))]
    private void ConfigureRepo(ModelHandlerBuilder<DependencyVersionReportModel, NoKey> builder)
    {
        builder
            .WithQueryable(() =>
                from dep in _dbContext.Dependencies
                let pkgVer = dep.PackageVersion
                let pkg = pkgVer.Package
                let manifest = dep.Manifest
                let repo = manifest.Repo
                let platform = pkg.Platform
                select new DependencyVersionReportModel
                {
                    RepoName = repo.Name,
                    PlatformName = platform.Name,
                    PkgName = pkg.Name,
                    CurrentVersion = pkgVer.Version,
                    LatestMajorVersion = _dbContext.PackageVersions
                        .Where(v => v.PackageId == pkg.Id && string.IsNullOrEmpty(v.PreRelease))
                        .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                        .Select(v => v.Version)
                        .FirstOrDefault(),
                    LatestMajorPrerelease = _dbContext.PackageVersions
                        .Where(v => v.PackageId == pkg.Id && !string.IsNullOrEmpty(v.PreRelease))
                        .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ThenByDescending(v => v.Patch).ThenByDescending(v => v.PreRelease)
                        .Select(v => v.Version)
                        .FirstOrDefault(),
                    LatestMinorVersion = _dbContext.PackageVersions
                        .Where(v => v.PackageId == pkg.Id && v.Major == pkgVer.Major && v.Minor == pkgVer.Minor && string.IsNullOrEmpty(v.PreRelease))
                        .OrderByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                        .Select(v => v.Version)
                        .FirstOrDefault(),
                    LatestMinorPrerelease = _dbContext.PackageVersions
                        .Where(v => v.PackageId == pkg.Id && v.Major == pkgVer.Major && v.Minor == pkgVer.Minor && !string.IsNullOrEmpty(v.PreRelease))
                        .OrderByDescending(v => v.Minor).ThenByDescending(v => v.Patch).ThenByDescending(v => v.PreRelease)
                        .Select(v => v.Version)
                        .FirstOrDefault()
                })
            .WithTable(table => table
                .AddSelectorColumn(x => x.RepoName)
                .AddSelectorColumn(x => x.PlatformName)
                .AddSelectorColumn(x => x.PkgName)
                .AddSelectorColumn(x => x.CurrentVersion)
                .AddSelectorColumn(x => x.LatestMajorVersion!)
                .AddSelectorColumn(x => x.LatestMinorVersion!)
                .AddSelectorColumn(x => x.LatestMajorPrerelease!)
                .AddSelectorColumn(x => x.LatestMinorPrerelease!)
            );
    }
}
