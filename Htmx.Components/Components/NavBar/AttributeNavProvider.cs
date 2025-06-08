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
        var grouped = descriptors.GroupBy(x => (
            // attributes are transient, so we must explicitly specify the values
            ActionAttr: (
                x.ActionAttr.Order,
                x.ActionAttr.DisplayName,
                x.ActionAttr.Icon,
                x.ActionAttr.HttpMethod,
                x.ActionAttr.PushUrl
            ),
            GroupAttr: (
                x.GroupAttr?.Order,
                x.GroupAttr?.DisplayName,
                x.GroupAttr?.Icon
            )
        ))
        .OrderBy(g => g.Key.GroupAttr.Order ?? g.Key.ActionAttr.Order);

        foreach (var group in grouped)
        {
            var (actionAttr, groupAttr) = group.Key;
            if (groupAttr.Order == null)
            {
                var desc = group.First();
                bool isAuthorized = await _authMetadataService.IsAuthorizedAsync(desc.Descriptor, user);

                if (!isAuthorized)
                    continue;

                builder.AddAction(BuildActionModelBuilder(desc));
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

    private static Action<ActionModelBuilder> BuildActionModelBuilder(NavActionDescriptor desc)
    {
        return a =>
        {
            a.WithIcon(desc.ActionAttr.Icon ?? "");
            a.WithLabel(desc.ActionAttr.DisplayName ?? desc.ActionName.Humanize());
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