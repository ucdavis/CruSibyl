using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Web.Middleware;
using Microsoft.AspNetCore.Authorization;

namespace CruSibyl.Web.Extensions;
public static class AuthorizationExtensions
{
    public static void AddAccessPolicy(this AuthorizationOptions options, string policy)
    {
        options.AddPolicy(policy, builder => builder.Requirements.Add(new VerifyRoleAccess(GetRoles(policy))));
    }

    public static string[] GetRoles(string accessCode)
    {
        return accessCode switch
        {
            AccessCodes.SystemAccess => new[] { Role.Codes.System },
            AccessCodes.AdminAccess => new[] { Role.Codes.System, Role.Codes.Admin },
            _ => throw new ArgumentException("Invalid access code", nameof(accessCode))
        };
    }
}

