using Htmx.Components.Models.Builders;

namespace CruSibyl.Web.Configuration;

public static class NavConfig
{
    public static Action<ActionSetBuilder> RegisterNavigation => actionSet =>
    {
        // we can use the ActionContext to get the current path or other context
        // var path = builder.ActionContext.HttpContext.Request.Path.ToString();
        actionSet.AddAction(action => action
            .WithLabel("Home")
            .WithIcon("fas fa-home")
            .WithHxGet("/Dashboard")
            .WithHxPushUrl())

        .AddGroup(group => group
            .WithLabel("Admin")
            .WithIcon("fas fa-cogs")
            .AddAction(action => action
                .WithLabel("Repos")
                .WithHxGet("/Admin")
                .WithHxPushUrl()));
    };
}