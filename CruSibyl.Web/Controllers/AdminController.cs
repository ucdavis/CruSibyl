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
        return Table();
    }

    public async Task<IActionResult> Table()
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

    public async Task<IActionResult> CancelEditTableRow()
    {
        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        var globalState = this.GetGlobalState();
        if (globalState.Get<bool>("Table", "EditingExistingRecord"))
        {
            var editingRepo = globalState.Get<Repo>("Table", "EditingRow")!;
            var originalRepo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == editingRepo.Id);

            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = originalRepo,
                Key = originalRepo.Id,
                RowType = RowType.ReadOnly,
            });
        }
        else
        {
            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = null!,
                StringKey = "new",
                RowType = RowType.Hidden,
            });
        }

        globalState.ClearKey("Table", "EditingRow");
        globalState.ClearKey("Table", "EditingExistingRecord");
        return _tableProvider.RefreshEditViews(tableModel);
    }


    public async Task<IActionResult> EditTableRow(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var globalState = this.GetGlobalState();
        globalState.Set("Table", "EditingRow", repo);
        globalState.Set("Table", "EditingExistingRecord", true);

        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            RowType = RowType.Editable,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> ReloadTableRow(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            RowType = RowType.ReadOnly,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    /// <summary>
    /// Reloads the table with the current query parameters.
    /// This is called when any action is performed on table sorting, filtering, or paging.
    /// </summary>
    /// <param name="query"></param>
    /// <returns>HTMX OOB swaps for all partial views</returns>
    public async Task<IActionResult> ReloadTable([FromQuery] TableQueryParams query)
    {
        var tableModel = await GetRepoData(query);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    public IActionResult NewTableRow()
    {
        var repo = new Repo
        {
            Id = 0,
            Name = string.Empty,
            Description = string.Empty,
        };

        var globalState = this.GetGlobalState();
        globalState.Set("Table", "EditingRow", repo);
        globalState.Set("Table", "EditingExistingRecord", false);

        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            RowType = RowType.Editable,
            StringKey = "new",
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    private async Task<TableModel<Repo, int>> GetRepoData(TableQueryParams query)
    {
        IQueryable<Repo> queryable = _dbContext.Repos.AsQueryable();

        var tableModel = await _tableProvider.BuildAndFetchPage(x => x.Id, queryable, query, GetTableConfig());

        return tableModel;
    }

    private static Action<TableModelBuilder<Repo, int>> GetTableConfig()
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
                            .WithHxGet($"/Admin/NewTableRow")
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
                        .WithHxPost($"/Admin/SaveTableRow"),

                    new ActionModel("Cancel")
                        .WithIcon("fas fa-times") // Font Awesome 5 icon for cancel
                        .WithHxGet($"/Admin/CancelEditTableRow")
                ]
                :
                [
                    new ActionModel("Edit")
                        .WithIcon("fas fa-edit") // Font Awesome 5 icon for edit
                        .WithHxGet($"/Admin/EditTableRow?key={row.Key}"),

                    new ActionModel("Delete")
                        .WithIcon("fas fa-trash") // Font Awesome 5 icon for delete
                        .WithClass("text-red-600")
                        .WithHxPost($"/Admin/DeleteTableRow?key={row.Key}")
                ]);
            });
    }
}
