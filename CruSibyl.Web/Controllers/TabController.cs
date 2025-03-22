using Htmx;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

public abstract class TabController : Controller
{
    protected IActionResult HandleTabRequest(string contentView)
    {
        if (Request.IsHtmx())
            return PartialView(contentView); // Return only the partial

        ViewData["InitialTab"] = Request.Path; // Set initial tab for full page load
        return View("~/Views/Shared/Index.cshtml"); // Use a single shared Index
    }
}