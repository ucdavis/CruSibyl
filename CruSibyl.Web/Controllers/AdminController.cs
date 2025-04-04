using Azure;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Htmx.Components.Table.Models;
using Htmx.Components.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Htmx.Components.Table;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class AdminController : TabController
{
    private readonly AppDbContext _dbContext;

    public AdminController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IActionResult Index() => HandleTabRequest("_Content");

    public async Task<IActionResult> LoadTable()
    {
        TableModel<Repo> tableModel = await GetData(new TableQueryParams()
        {
            PageSize = 2
        });

        return PartialView("_Table", tableModel.ToNonGeneric());
    }


    public async Task<IActionResult> LoadData([FromQuery] TableQueryParams query)
    {
        var tableModel = (await GetData(query)).ToNonGeneric();

        return new RefreshTableViewResult(tableModel);
    }

    private async Task<TableModel<Repo>> GetData(TableQueryParams query)
    {
        IQueryable<Repo> queryable = _dbContext.Repos.AsQueryable();

        var tableModel = await new TableModelBuilder<Repo>(queryable, query)
            .AddHiddenColumn("Id", x => x.Id)
            .AddSelectorColumn("Name", x => x.Name, config => config
                .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
            .AddSelectorColumn("Description", x => x.Description!)
            .BuildAsync();

        return tableModel;
    }

}
