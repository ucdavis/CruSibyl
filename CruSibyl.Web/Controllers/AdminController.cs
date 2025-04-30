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
using Htmx.Components;

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

    public Task<IActionResult> Index()
    {
        return RepoTable();
    }

    public async Task<IActionResult> RepoTable()
    {
        var globalState = this.GetGlobalState();
        var test = globalState.Get<int>("AdminTab", "Test");
        globalState.Set("AdminTab", "Test", test + 1);

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
            RowType = RowType.Editable,
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
            RowType = RowType.ReadOnly,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> ReloadRepoTable([FromQuery] TableQueryParams query)
    {
        var tableModel = await GetRepoData(query);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    public IActionResult NewRepo()
    {
        var tableModel = _tableProvider.Build(r => r.Id, GetRepoTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = new Repo(),
            RowType = RowType.Editable,
            StringKey = "new",
        });

        return _tableProvider.RefreshEditViews(tableModel);
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
                var isEditing = table.Rows.Any(r => r.RowType == RowType.Editable);
                return isEditing
                    ? []
                    : [
                        new ActionModel("Add New")
                            .WithIcon("fas fa-plus mr-1")
                            .WithHxGet($"/Admin/NewRepo")
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
                row.RowType == RowType.Editable ?
                [
                    new ActionModel("Save")
                        .WithIcon("fas fa-save") // Font Awesome 5 icon for save
                        .WithHxPost($"/Admin/SaveRepo?key={row.Key}"),

                    new ActionModel("Cancel")
                        .WithIcon("fas fa-times") // Font Awesome 5 icon for cancel
                        .WithHxGet($"/Admin/ReloadRepoTableRow?key={row.Key}")
                ]
                :
                [
                    new ActionModel("Edit")
                        .WithIcon("fas fa-edit") // Font Awesome 5 icon for edit
                        .WithHxGet($"/Admin/EditRepoTableRow?key={row.Key}"),

                    new ActionModel("Delete")
                        .WithIcon("fas fa-trash") // Font Awesome 5 icon for delete
                        .WithClass("text-red-600")
                        .WithHxPost($"/Admin/DeleteRepo?key={row.Key}")
                ]);
            });
    }
}
