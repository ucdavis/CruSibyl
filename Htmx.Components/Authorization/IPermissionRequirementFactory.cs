using Microsoft.AspNetCore.Authorization;

namespace Htmx.Components.Authorization;

/// <summary>
/// Factory for creating authorization requirements based on roles or on resource and operation.
/// </summary>
public interface IPermissionRequirementFactory
{
    IAuthorizationRequirement ForOperation(string resource, string operation);
    IAuthorizationRequirement ForRoles(params string[] roles);
}
