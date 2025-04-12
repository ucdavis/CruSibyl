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
                .WithOob("_Content", tableModel.ToNonGeneric())
                .BuildAsync();
        }

        return RenderInitialMainContent("_Content", tableModel.ToNonGeneric());
    }

    public async Task<IActionResult> LoadTable()
    {
        TableModel<Repo, int> tableModel = await GetData(new TableQueryParams()
        {
            PageSize = 2
        });

        return PartialView("_Table", tableModel.ToNonGeneric());
    }


    public async Task<IActionResult> LoadData([FromQuery] TableQueryParams query)
    {
        var tableModel = (await GetData(query)).ToNonGeneric();

        return _tableProvider.RefreshView(tableModel);
    }

    private async Task<TableModel<Repo, int>> GetData(TableQueryParams query)
    {
        IQueryable<Repo> queryable = _dbContext.Repos.AsQueryable();

        var tableModel = await _tableProvider.BuildAndFetchPage(x => x.Id, queryable, query, config => config
            .AddHiddenColumn("Id", x => x.Id)
            .AddSelectorColumn("Name", x => x.Name, config => config
                .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
            .AddSelectorColumn("Description", x => x.Description!)
            .AddDisplayColumn("Actions", col =>
            {
                col.WithActions(row =>
                [
                    new ActionModelBuilder()
                        .WithLabel("Edit")
                        .WithIcon("edit")
                        .WithHxGet($"/Admin/EditRepo?key={row.Key}&rowId={row.RowId}")
                        .Build(),

                    new ActionModelBuilder()
                        .WithLabel("Delete")
                        .WithIcon("trash")
                        .WithClass("text-red-600") // TODO: tailwind won't pick up stuff like this
                        .WithHxPost($"/Admin/DeleteRepo?key={row.Key}&rowId={row.RowId}")
                        .Build()
                ]);
            }));

        return tableModel;
    }

}
