using Htmx.Components.Models.Table;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.Table;

public class TableViewComponent : ViewComponent
{
    private readonly TableViewPaths _viewPaths;

    public TableViewComponent(TableViewPaths viewPaths)
    {
        _viewPaths = viewPaths;
    }

    public IViewComponentResult Invoke(ITableModel model)
    {
        model.TableViewPaths = _viewPaths;

        return View(_viewPaths.Table, model);
    }
}