using System.Security.Claims;

namespace Htmx.Components.Services;

public interface IRoleService
{
    /// <summary>
    /// Returns true if the user has at least one of the required roles.
    /// </summary>
    Task<bool> UserHasAnyRoleAsync(ClaimsPrincipal user, IEnumerable<string> requiredRoles);
}