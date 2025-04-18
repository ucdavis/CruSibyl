using Azure;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Htmx.Components.Table.Models;
using Htmx.Components.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Htmx.Components.Table;
using Htmx.Components.Action;
using Htmx;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class AdminController : TabController
{
    private readonly AppDbContext _dbContext;
    private readonly ITableProvider _tableProvider;

    public AdminController(AppDbContext dbContext, ITableProvider tableProvider)
    {
        _dbContext = dbContext;
        _tableProvider = tableProvider;
    }

    public async Task<IActionResult> Index()
    {
        TableModel<Repo, int> tableModel = await GetData(new TableQueryParams()
        {
            PageSize = 2
        });

        if (Request.IsHtmx())
        {
            return await HtmxResultBuilder
                .WithOobNavbar()
                .WithOob("_Content", tableModel.ToViewModel())
                .BuildAsync();
        }

        return RenderInitialMainContent("_Content", tableModel.ToViewModel());
    }

    public async Task<IActionResult> EditRepo(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var tableModel = _tableProvider.Build(r => r.Id, GetRepoConfig());
        tableModel.Data.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            IsEditing = true
        });

        return _tableProvider.RefreshEditViews(tableModel.ToViewModel());
    }

    public async Task<IActionResult> ReloadRepo(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var tableModel = _tableProvider.Build(r => r.Id, GetRepoConfig());
        tableModel.Data.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            IsEditing = false
        });
        var viewModel = tableModel.ToViewModel();

        return _tableProvider.RefreshEditViews(viewModel);
    }    

    public async Task<IActionResult> LoadData([FromQuery] TableQueryParams query)
    {
        var tableModel = (await GetData(query)).ToViewModel();

        return _tableProvider.RefreshAllViews(tableModel);
    }

    private async Task<TableModel<Repo, int>> GetData(TableQueryParams query)
    {
        IQueryable<Repo> queryable = _dbContext.Repos.AsQueryable();

        var tableModel = await _tableProvider.BuildAndFetchPage(x => x.Id, queryable, query, GetRepoConfig());

        return tableModel;
    }

    private static Action<TableModelBuilder<Repo, int>> GetRepoConfig()
    {
        return config => config
            .AddHiddenColumn("Id", x => x.Id)
            .AddSelectorColumn("Name", x => x.Name, config => config
                .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
            .AddSelectorColumn("Description", x => x.Description!)
            .AddDisplayColumn("Actions", col =>
            {
                col.WithActions(row =>
                row.IsEditing ?
                [
                    new ActionModelBuilder()
                        .WithLabel("Save")
                        //.WithIcon("trash")
                        .WithHxPost($"/Admin/DeleteRepo?key={row.Key}")
                        .Build(),

                    new ActionModelBuilder()
                        .WithLabel("Cancel")
                        //.WithIcon("trash")
                        .WithHxGet($"/Admin/ReloadRepo?key={row.Key}")
                        .Build()
                ]
                :
                [
                    new ActionModelBuilder()
                        .WithLabel("Edit")
                        .WithIcon("edit")
                        .WithHxGet($"/Admin/EditRepo?key={row.Key}")
                        .Build(),

                    new ActionModelBuilder()
                        .WithLabel("Delete")
                        .WithIcon("trash")
                        .WithClass("text-red-600") // TODO: tailwind won't pick up stuff like this
                        .WithHxPost($"/Admin/DeleteRepo?key={row.Key}")
                        .Build()
                ]);
            });
    }
}
