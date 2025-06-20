using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.Table;

public class TableViewComponent : ViewComponent
{
    private readonly ViewPaths _viewPaths;

    public TableViewComponent(ViewPaths viewPaths)
    {
        _viewPaths = viewPaths;
    }

    public IViewComponentResult Invoke(ITableModel model)
    {
        model.TableViewPaths = _viewPaths.Table;

        return View(_viewPaths.Table.Table, model);
    }
}