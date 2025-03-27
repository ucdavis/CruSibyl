
using CruSibyl.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Components;

public class TableViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(TableModel<dynamic> model)
    {
        return View("TableView", model);
    }
}
