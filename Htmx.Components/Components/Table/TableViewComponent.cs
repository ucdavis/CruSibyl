using Htmx.Components.Table.Models;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.Table;

public class TableViewComponent : ViewComponent
{
    private readonly TableViewPaths _viewPaths;

    public TableViewComponent(TableViewPaths viewPaths)
    {
        _viewPaths = viewPaths;
    }

    public IViewComponentResult Invoke(TableModel model)
    {
        ViewData["BodyPartial"] = _viewPaths.Body;
        ViewData["CellActionListPartial"] = _viewPaths.CellActionList;
        ViewData["FilterDateRangePartial"] = _viewPaths.FilterDateRange;
        ViewData["FilterTextPartial"] = _viewPaths.FilterText;
        ViewData["HeaderPartial"] = _viewPaths.Header;
        ViewData["PaginationPartial"] = _viewPaths.Pagination;
        ViewData["HiddenValuesPartial"] = _viewPaths.HiddenValues;

        return View(_viewPaths.Table, model);
    }
}