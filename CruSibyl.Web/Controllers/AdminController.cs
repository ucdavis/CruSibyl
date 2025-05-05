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
        var pageState = this.GetPageState();
        var test = pageState.Get<int>("AdminTab", "Test");
        pageState.Set("AdminTab", "Test", test + 1);
        var tableState = new TableState()
        {
            PageSize = 2
        };
        pageState.Set("Table", "State", tableState);

        TableModel<Repo, int> tableModel = await GetRepoData(tableState);

        if (Request.IsHtmx())
        {
            return await HtmxResultBuilder
                .WithOobNavbar()
                .WithOob("_Content", tableModel)
                .BuildAsync();
        }

        return RenderInitialMainContent("_Content", tableModel);
    }

    public async Task<IActionResult> SaveTableRow()
    {
        var pageState = this.GetPageState();
        var editingRepo = pageState.Get<Repo>("Table", "EditingRow")!;
        var editingExistingRecord = pageState.Get<bool>("Table", "EditingExistingRecord")!;
        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        if (editingExistingRecord)
        {
            _dbContext.Repos.Update(editingRepo);
            await _dbContext.SaveChangesAsync();
            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = editingRepo,
                Key = editingRepo.Id,
                RowAction = RowAction.Display,
            });
        }
        else
        {
            _dbContext.Repos.Add(editingRepo);
            await _dbContext.SaveChangesAsync();
            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = null!,
                StringKey = "new",
                RowAction = RowAction.Delete,
            });
            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = editingRepo,
                Key = editingRepo.Id,
                RowAction = RowAction.Insert,
            });
        }


        pageState.ClearKey("Table", "EditingRow");
        pageState.ClearKey("Table", "EditingExistingRecord");

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> CancelEditTableRow()
    {
        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        var pageState = this.GetPageState();
        if (pageState.Get<bool>("Table", "EditingExistingRecord"))
        {
            var editingRepo = pageState.Get<Repo>("Table", "EditingRow")!;
            var originalRepo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == editingRepo.Id);

            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = originalRepo,
                Key = originalRepo.Id,
                RowAction = RowAction.Display,
            });
        }
        else
        {
            tableModel.Rows.Add(new TableRowContext<Repo, int>
            {
                Item = null!,
                StringKey = "new",
                RowAction = RowAction.Delete,
            });
        }

        pageState.ClearKey("Table", "EditingRow");
        pageState.ClearKey("Table", "EditingExistingRecord");
        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> DeleteTableRow(int key)
    {
        var repo = await _dbContext.Repos.SingleAsync(r => r.Id == key);
        _dbContext.Repos.Remove(repo);
        await _dbContext.SaveChangesAsync();

        var pageState = this.GetPageState();
        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            RowAction = RowAction.Delete,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> EditTableRow(int key)
    {
        var repo = await _dbContext.Repos.AsNoTracking().SingleAsync(r => r.Id == key);
        var pageState = this.GetPageState();
        pageState.Set("Table", "EditingRow", repo);
        pageState.Set("Table", "EditingExistingRecord", true);

        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            Key = key,
            RowAction = RowAction.Edit,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    public async Task<IActionResult> SetPage(int page)
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.Page = page;
        pageState.Set("Table", "State", tableState);
        var tableModel = await GetRepoData(tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    public async Task<IActionResult> SetPageSize(int pageSize)
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.PageSize = pageSize;
        pageState.Set("Table", "State", tableState);
        var tableModel = await GetRepoData(tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    public async Task<IActionResult> SetSort(string column, string direction)
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.SortColumn = column;
        tableState.SortDirection = direction;
        pageState.Set("Table", "State", tableState);
        var tableModel = await GetRepoData(tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    public IActionResult SetCell(string propertyName, string value)
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        var editingRow = pageState.Get<Repo>("Table", "EditingRow")!;
        var property = typeof(Repo).GetProperty(propertyName);
        if (property == null)
            return BadRequest($"Property '{propertyName}' not found.");

        try
        {
            var convertedValue = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(editingRow, convertedValue);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to set property '{propertyName}': {ex.Message}");
        }

        pageState.Set("Table", "EditingRow", editingRow);
        return new MultiSwapViewResult();
    }

    public async Task<IActionResult> SetFilter(string column, string filter, int input)
    {
        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        var columnModel = tableModel.Columns.FirstOrDefault(c => c.DataName == column);
        if (columnModel == null)
            return BadRequest($"Column '{column}' not found.");

        if (!columnModel.Filterable || (columnModel.RangeFilter == null && columnModel.Filter == null))
            return BadRequest($"Column '{column}' is not filterable.");

        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        if (columnModel.Filter != null)
        {
            if (string.IsNullOrEmpty(filter))
                tableState.Filters.Remove(column);
            else
                tableState.Filters[column] = filter;
        }
        else if (columnModel.RangeFilter != null)
        {
            (var from, var to) = tableState.RangeFilters.TryGetValue(column, out var range) ? range : ("", "");
            if (input == 1)
                from = filter;
            else if (input == 2)
                to = filter;
            else
                return BadRequest($"Invalid input value: {input}");
            tableState.RangeFilters[column] = (from, to);
        }

        pageState.Set("Table", "State", tableState);
        await _tableProvider.FetchPage(tableModel, _dbContext.Repos, tableState);
        return _tableProvider.RefreshAllViews(tableModel);
    }

    /// <summary>
    /// Reloads the table with the current query parameters.
    /// This is called when any action is performed on table sorting, filtering, or paging.
    /// </summary>
    /// <param name="state"></param>
    /// <returns>HTMX OOB swaps for all partial views</returns>
    public async Task<IActionResult> ReloadTable([FromQuery] TableState state)
    {
        var pageState = this.GetPageState();
        pageState.Set("Table", "State", state);
        var tableModel = await GetRepoData(state);

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

        var pageState = this.GetPageState();
        pageState.Set("Table", "EditingRow", repo);
        pageState.Set("Table", "EditingExistingRecord", false);

        var tableModel = _tableProvider.Build(r => r.Id, GetTableConfig());
        tableModel.Rows.Add(new TableRowContext<Repo, int>
        {
            Item = repo,
            RowAction = RowAction.Insert,
            StringKey = "new",
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    private async Task<TableModel<Repo, int>> GetRepoData(TableState query)
    {
        var tableModel = await _tableProvider.BuildAndFetchPage(x => x.Id, _dbContext.Repos, query, GetTableConfig());

        return tableModel;
    }

    private static Action<TableModelBuilder<Repo, int>> GetTableConfig()
    {
        return config => config
            .WithActions(table => [
                new ActionModel("Add New")
                    .WithIcon("fas fa-plus mr-1")
                    .WithHxGet($"/Admin/NewTableRow")
            ])
            .AddSelectorColumn("Name", x => x.Name, config => config
                .WithEditable()
                .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
            .AddSelectorColumn("Description", x => x.Description!, config => config
                .WithEditable())
            .AddDisplayColumn("Actions", col =>
            {
                col.WithActions(row =>
                row.RowAction == RowAction.Edit || row.RowAction == RowAction.Insert ?
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
