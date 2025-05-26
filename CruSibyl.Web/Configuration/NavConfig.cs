using Htmx.Components.Models.Builders;

namespace CruSibyl.Web.Configuration;

public static class NavConfig
{
    public static Action<ActionSetBuilder> RegisterNavigation => builder =>
    {
        // we can use the ActionContext to get the current path or other context
        // var path = builder.ActionContext.HttpContext.Request.Path.ToString();
        builder.AddModel(m => m
            .WithLabel("Home")
            .WithIcon("fas fa-home")
            .WithHxGet("/Dashboard")
            .WithHxPushUrl())

        .AddGroup(g => g
            .WithLabel("Admin")
            .WithIcon("fas fa-cogs")
            .AddModel(m => m
                .WithLabel("Repos")
                .WithHxGet("/Admin")
                .WithHxPushUrl()));
    };
}