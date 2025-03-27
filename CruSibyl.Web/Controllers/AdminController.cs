using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public IActionResult LoadData([FromQuery] TableQueryParams query)
    {
        var columns = GetRepoColumns();

        IQueryable<Repo> data = _dbContext.Repos.AsQueryable();

        // **Apply Filters**
    if (query.Filters != null)
    {
        foreach (var filter in query.Filters)
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
        foreach (var rangeFilter in query.RangeFilters)
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

        // **Apply Pagination**
        var paginatedData = data.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var tableModel = new TableModel<Repo>
        {
            Data = paginatedData,
            Columns = columns,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = data.Count(),
            Sort = query.SortColumn,
            SortDirection = query.SortDirection
        };

        return PartialView("_TableView", tableModel.ToNonGeneric());
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
