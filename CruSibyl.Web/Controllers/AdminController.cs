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

namespace CruSibyl.Web.Controllers;

[Authorize]
[Route("Admin")]
public class AdminController : TabController
{
    private readonly AppDbContext _dbContext;
    private readonly ITableProvider _tableProvider;
    private readonly IModelRegistry _modelRegistry;

    public AdminController(AppDbContext dbContext, ITableProvider tableProvider, IModelRegistry modelRegistry)
    {
        _dbContext = dbContext;
        _tableProvider = tableProvider;
        _modelRegistry = modelRegistry;
    }

    [HttpGet]
    public Task<IActionResult> Index()
    {
        return Table();
    }

    [HttpGet("Index")]
    public async Task<IActionResult> Table()
    {
        var pageState = this.GetPageState();
        var tableState = new TableState();
        pageState.Set("Table", "State", tableState);

        var modelHandler = await _modelRegistry.GetModelHandler<Repo, int>(nameof(Repo), ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModel();
        await _tableProvider.FetchPage(tableModel, _dbContext.Repos, tableState);

        if (Request.IsHtmx())
        {
            return await HtmxResultBuilder
                .WithOobNavbar()
                .WithOob("_Content", tableModel)
                .BuildAsync();
        }

        return RenderInitialMainContent("_Content", tableModel);
    }
}
