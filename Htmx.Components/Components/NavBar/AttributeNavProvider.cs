using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Htmx.Components.Attributes;
using Htmx.Components.Models.Builders;
using Microsoft.AspNetCore.Http;
using Htmx.Components.Models;
using Htmx.Components.Services;
using Humanizer;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel;

namespace Htmx.Components.NavBar;

public class AttributeNavProvider : INavProvider
{
    private readonly IActionDescriptorCollectionProvider _actions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthorizationMetadataService _authMetadataService;

    public AttributeNavProvider(
        IActionDescriptorCollectionProvider actions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IAuthorizationMetadataService authMetadataService)
    {
        _actions = actions;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _authMetadataService = authMetadataService;
    }

    public async Task<IActionSet> BuildAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            // If no user is available, return an empty ActionSet
            return new ActionSet(new ActionSetConfig());
        }
        var builder = new ActionSetBuilder(_serviceProvider);

        var descriptors = await Task.WhenAll(
            _actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                .Where(desc => desc.MethodInfo.GetCustomAttribute<NavActionAttribute>() != null)
                .Select(async desc =>
                {
                    var meta = await _authMetadataService.GetMetadataAsync(desc);
                    return new NavActionDescriptor
                    {
                        MethodInfo = desc.MethodInfo,
                        ControllerTypeInfo = desc.ControllerTypeInfo,
                        ActionName = desc.ActionName,
                        ControllerName = desc.ControllerName,
                        ActionAttr = desc.MethodInfo.GetCustomAttribute<NavActionAttribute>()!,
                        GroupAttr = desc.MethodInfo.GetCustomAttribute<NavActionGroupAttribute>()
                            ?? desc.ControllerTypeInfo.GetCustomAttribute<NavActionGroupAttribute>(),
                        Metadata = meta,
                        Descriptor = desc
                    };
                })
        );
        var grouped = descriptors
        .GroupBy(x => (
            Order: x.GroupAttr != null ? x.GroupAttr.Order : x.ActionAttr.Order,
            DisplayNameAttribute: x.GroupAttr != null ? x.GroupAttr.DisplayName : x.ActionAttr.DisplayName,
            Icon: x.GroupAttr != null ? x.GroupAttr.Icon : x.ActionAttr.Icon
        ))
        .OrderBy(g => g.Key.Order);

        foreach (var group in grouped)
        {
            if (group.Count() == 1 && group.First().GroupAttr == null)
            {
                // If there's only one action and no group attribute, add it directly
                var desc = group.First();
                if (!await _authMetadataService.IsAuthorizedAsync(desc.Descriptor, user))
                    continue;

                builder.AddAction(BuildActionModelBuilder(desc));
                continue;
            }
            else
            {
                var actionModelBuilders = new List<Action<ActionModelBuilder>>();
                foreach (var desc in group)
                {
                    if (!await _authMetadataService.IsAuthorizedAsync(desc.Descriptor, user))
                        continue;
                    actionModelBuilders.Add(BuildActionModelBuilder(desc));
                }

                if (!actionModelBuilders.Any())
                    continue;

                builder.AddGroup(g =>
                {
                    var groupAttr = group.First().GroupAttr!;
                    g.WithLabel(groupAttr.DisplayName ?? groupAttr.DisplayName ?? group.First().ControllerName);
                    g.WithIcon(groupAttr.Icon ?? "");
                    foreach (var actionBuilder in actionModelBuilders)
                    {
                        g.AddAction(actionBuilder);
                    }
                });
            }
        }

        return await builder.Build();
    }

    private Action<ActionModelBuilder> BuildActionModelBuilder(NavActionDescriptor desc)
    {
        // Get current route info
        var httpContext = _httpContextAccessor.HttpContext;
        var routeData = httpContext?.GetRouteData();
        var currentController = routeData?.Values["controller"]?.ToString();
        var currentAction = routeData?.Values["action"]?.ToString();
        var isActive = string.Equals(desc.ControllerName, currentController, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(desc.ActionName, currentAction, StringComparison.OrdinalIgnoreCase);

        return a =>
        {
            a.WithIcon(desc.ActionAttr.Icon ?? "");
            a.WithIsActive(isActive);
            a.WithLabel(desc.ActionAttr.DisplayName ?? desc.ActionName.Humanize(LetterCasing.Title));
            // We're assuming an action named "Index" is the default action for the controller
            string url = desc.ActionName.Equals("Index", StringComparison.OrdinalIgnoreCase)
                ? $"/{desc.ControllerName}"
                : $"/{desc.ControllerName}/{desc.ActionName}";

            if (string.Equals(desc.ActionAttr.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                a.WithHxPost(url);
            else
                a.WithHxGet(url);
            a.WithHxPushUrl(desc.ActionAttr.PushUrl.ToString().ToLowerInvariant());
        };
    }

    private class NavActionDescriptor
    {
        public MethodInfo MethodInfo { get; set; } = default!;
        public TypeInfo ControllerTypeInfo { get; set; } = default!;
        public string ActionName { get; set; } = "";
        public string ControllerName { get; set; } = "";
        public NavActionAttribute ActionAttr { get; set; } = default!;
        public NavActionGroupAttribute? GroupAttr { get; set; }
        public object? Metadata { get; set; }
        public ControllerActionDescriptor Descriptor { get; set; } = default!;
    }
}