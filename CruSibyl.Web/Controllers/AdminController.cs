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

    public Task<IActionResult> Index() => RepoTable();

    public async Task<IActionResult> RepoTable()
    {
        TableModel<Repo, int> tableModel = await GetRepoData(new TableQueryParams()
        {
            PageSize = 2
        });

        if (Request.IsHtmx())
        {
            return await HtmxResultBuilder
                .WithOobNavbar()
                .WithOob("_Content", tableModel)
                .BuildAsync();
        }

        return RenderInitialMainContent("_Content", tableModel);
    }

    public async Task<IActionResult> EditRepoTableRow(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var tableModel = _tableProvider.Build(r => r.Id, GetRepoTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            IsEditing = true
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> ReloadRepoTableRow(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var tableModel = _tableProvider.Build(r => r.Id, GetRepoTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            IsEditing = false
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> ReloadRepoTable([FromQuery] TableQueryParams query)
    {
        var tableModel = await GetRepoData(query);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    private async Task<TableModel<Repo, int>> GetRepoData(TableQueryParams query)
    {
        IQueryable<Repo> queryable = _dbContext.Repos.AsQueryable();

        var tableModel = await _tableProvider.BuildAndFetchPage(x => x.Id, queryable, query, GetRepoTableConfig());

        return tableModel;
    }

    private static Action<TableModelBuilder<Repo, int>> GetRepoTableConfig()
    {
        return config => config
            .WithActions(table =>
            {
                var isEditing = table.Rows.Any(r => r.IsEditing);
                return isEditing
                    ? []
                    : [
                        new ActionModelBuilder()
                            .WithLabel("Add New")
                            .WithIcon("fas fa-plus mr-1")
                            .WithHxGet($"/Admin/NewRepo")
                            .Build()
                    ];
            })
            .AddHiddenColumn("Id", x => x.Id)
            .AddSelectorColumn("Name", x => x.Name, config => config
                .WithEditable()
                .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
            .AddSelectorColumn("Description", x => x.Description!, config => config
                .WithEditable())
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
                        .WithHxGet($"/Admin/ReloadRepoTableRow?key={row.Key}")
                        .Build()
                ]
                :
                [
                    new ActionModelBuilder()
                        .WithLabel("Edit")
                        .WithIcon("edit")
                        .WithHxGet($"/Admin/EditRepoTableRow?key={row.Key}")
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
