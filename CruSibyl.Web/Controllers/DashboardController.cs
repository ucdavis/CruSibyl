﻿using Htmx;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

public class DashboardController : TabController
{
    public async Task<IActionResult> Index()
    {
        if (Request.IsHtmx())
        {
            return await HtmxResultBuilder
                .WithUpdatedNavbar()
                .WithUpdatedNavContent("_Content", new { })
                .BuildAsync();
        }

        return RenderInitialTabContent("_Content", new { });
    }
}
