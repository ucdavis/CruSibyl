﻿using Azure;
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
    private readonly IModelHandlerFactoryGeneric _modelHandlerFactory;


    public AdminController(AppDbContext dbContext, ITableProvider tableProvider, IModelHandlerFactoryGeneric modelHandlerFactory)
    {
        _dbContext = dbContext;
        _tableProvider = tableProvider;
        _modelHandlerFactory = modelHandlerFactory;
    }

    [HttpGet]
    [NavAction(DisplayName = "Repos", Icon = "fas fa-database", Order = 0, PushUrl = true, ViewName = "_Content")]
    public async Task<IActionResult> Index()
    {
        var pageState = this.GetPageState();
        var tableState = new TableState();
        pageState.Set("Table", "State", tableState);

        var modelHandler = await _modelHandlerFactory.Get<Repo, int>(nameof(Repo), ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModel();
        await _tableProvider.FetchPage(tableModel, _dbContext.Repos, tableState);

        return Ok(tableModel);
    }



    [ModelConfig("Repo")]
    private void ConfigureRepo(ModelHandlerBuilder<Repo, int> builder)
    {
        builder
            .WithKeySelector(r => r.Id)
            .WithQueryable(() => _dbContext.Repos)
            .WithCreate(async repo =>
            {
                _dbContext.Repos.Add(repo);
                await _dbContext.SaveChangesAsync();
                return Htmx.Components.Models.Result.Ok();
            })
            .WithUpdate(async repo =>
            {
                _dbContext.Repos.Update(repo);
                await _dbContext.SaveChangesAsync();
                return Htmx.Components.Models.Result.Ok();
            })
            .WithDelete(async id =>
            {
                var repo = await _dbContext.Repos.FindAsync(id);
                if (repo != null)
                {
                    _dbContext.Repos.Remove(repo);
                    await _dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                }
                return Htmx.Components.Models.Result.Error("Repo not found");
            })
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
