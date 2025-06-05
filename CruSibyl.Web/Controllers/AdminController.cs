using Azure;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Htmx.Components.Table;
using Htmx;
using Htmx.Components;
using Htmx.Components.Models;
using Htmx.Components.ViewResults;
using Htmx.Components.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text.Json;
using Htmx.Components.Models.Table;
using Htmx.Components.Attributes;
using Htmx.Components.Models.Builders;
using Result = Htmx.Components.Models.Result;

namespace CruSibyl.Web.Controllers;

[Authorize]
[Route("Admin")]
[NavActionGroup(DisplayName = "Admin", Icon = "fas fa-cogs", Order = 1)]
public class AdminController : TabController
{
    private readonly AppDbContext _dbContext;
    private readonly ITableProvider _tableProvider;

    public AdminController(AppDbContext dbContext, ITableProvider tableProvider)
    {
        _dbContext = dbContext;
        _tableProvider = tableProvider;
    }

    [HttpGet]
    [NavAction(DisplayName = "Repos", Icon = "fas fa-database", Order = 0, PushUrl = true, ViewName = "_Content")]
    public async Task<IActionResult> Index([FromServices] IModelRegistry modelRegistry)
    {
        var pageState = this.GetPageState();
        var tableState = new TableState();
        pageState.Set("Table", "State", tableState);

        var modelHandler = await modelRegistry.GetModelHandler<Repo, int>(nameof(Repo), ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModel();
        await _tableProvider.FetchPage(tableModel, _dbContext.Repos, tableState);

        return Ok(tableModel);
    }

    [ModelCreate]
    private async Task<Result> CreateRepoAsync(Repo repo)
    {
        if (string.IsNullOrWhiteSpace(repo.Name))
        {
            return Result.Error("Repo name is required.");
        }

        _dbContext.Repos.Add(repo);
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    [ModelRead]
    private IQueryable<Repo> ReadRepos()
    { 
        return _dbContext.Repos.AsNoTracking();
    }

    [ModelUpdate]
    private async Task<Result> UpdateRepoAsync(Repo repo)
    { 
        if (string.IsNullOrWhiteSpace(repo.Name))
        {
            return Result.Error("Repo name is required.");
        }

        _dbContext.Repos.Update(repo);
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    [ModelDelete]
    private async Task<Result> DeleteRepoAsync(int id)
    { 
        var repo = await _dbContext.Repos.FindAsync(id);
        if (repo == null)
        {
            return Result.Error("Repo not found.");
        }

        _dbContext.Repos.Remove(repo);
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    [ModelConfig]
    private void ConfigureRepo(ModelHandlerBuilder<Repo, int> builder)
    {
        builder
            .WithKeySelector(r => r.Id)
            .WithInput(r => r.Name, config => config
                    .WithLabel("Name")
                    .WithPlaceholder("Enter repo name")
                    .WithCssClass("form-control"))
                .WithInput(r => r.Description, config => config
                    .WithLabel("Description")
                    .WithPlaceholder("Enter repo description")
                    .WithCssClass("form-control"))
                .WithTable(table => table
                    .WithCrudActions()
                    .AddSelectorColumn("Name", x => x.Name, config => config
                        .WithEditable()
                        .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
                    .AddSelectorColumn("Description", x => x.Description!, config => config
                        .WithEditable())
                    .AddCrudDisplayColumn());
    }
}
