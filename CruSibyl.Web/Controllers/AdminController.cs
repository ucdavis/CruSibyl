using Azure;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Web.Models;
using CruSibyl.Web.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class AdminController : TabController
{
    private readonly AppDbContext _dbContext;

    public AdminController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    static int pageNum = 0;


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

        return new MultiSwapViewResult(
            ("_TableBody", tableModel),
            ("_TablePagination", tableModel),
            ("_TableHeader", tableModel)
        );
    }

    private async Task<TableModel<Repo>> GetData(TableQueryParams query)
    {
        var columns = GetRepoColumns();

        IQueryable<Repo> data = _dbContext.Repos.AsQueryable();

        // **Apply Filters**
        if (query.Filters != null)
        {
            foreach (var filter in query.Filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
            {
                var column = columns.FirstOrDefault(c => c.Header == filter.Key);
                if (column?.FilterExpression != null)
                {
                    data = column.FilterExpression(data, filter.Value);
                }
            }
        }

        // **Apply Range Filters**
        if (query.RangeFilters != null)
        {
            foreach (var rangeFilter in query.RangeFilters.Where(f => !string.IsNullOrWhiteSpace(f.Value.Min)
                    && !string.IsNullOrWhiteSpace(f.Value.Max)))
            {
                var column = columns.FirstOrDefault(c => c.Header == rangeFilter.Key);
                if (column?.RangeFilterExpression != null)
                {
                    data = column.RangeFilterExpression(data, rangeFilter.Value.Min, rangeFilter.Value.Max);
                }
            }
        }

        // **Apply Sorting**
        if (!string.IsNullOrEmpty(query.SortColumn))
        {
            var column = columns.FirstOrDefault(c => c.Header == query.SortColumn);
            if (column != null)
            {
                data = query.SortDirection == "asc"
                    ? data.OrderBy(column.ValueExpression)
                    : data.OrderByDescending(column.ValueExpression);
            }
        }

        // **Get Total Count Before Pagination**
        var totalCount = await data.CountAsync();

        // **Apply Pagination**
        var paginatedData = await data.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();


        var tableModel = new TableModel<Repo>
        {
            Data = paginatedData,
            Columns = columns,
            PageCount = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize)),
            Query = query
        };
        return tableModel;
    }

    private List<TableColumnModel<Repo>> GetRepoColumns()
    {
        return new List<TableColumnModel<Repo>>
        {
            new()
            {
                Header = "Name",
                ValueExpression = data => data.Name,
                Sortable = true,
                Filterable = true,
                FilterExpression = (query, value) => query.Where(d => d.Name.Contains(value)),
                FilterPartialView = "_TableFilterText"
            },
            new()
            {
                Header = "Description",
                ValueExpression = data => data.Description ?? "",
                Sortable = true,
                Filterable = true,
                FilterExpression = (query, value) => query.Where(d => d.Description != null && d.Description.Contains(value)),
                FilterPartialView = "_TableFilterText"
            }
        };
    }
}
