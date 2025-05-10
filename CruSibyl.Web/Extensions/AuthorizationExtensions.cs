using CruSibyl.Core.Models;
using CruSibyl.Web.Middleware;
using Microsoft.AspNetCore.Authorization;

namespace CruSibyl.Web.Extensions;

public static class AuthorizationExtensions
{
    public static void AddAccessPolicy(this AuthorizationOptions options, string policy)
    {
        options.AddPolicy(policy, builder => builder.Requirements.Add(PermissionRequirement.ForRoles(AccessPolicies.GetRoles(policy))));
    }
}

