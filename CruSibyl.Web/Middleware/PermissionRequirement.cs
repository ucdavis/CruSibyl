using Htmx.Components.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CruSibyl.Web.Middleware;

public class PermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string>? AllowedRoles { get; }
    public string? Resource { get; }
    public string? Operation { get; }

    private PermissionRequirement(IReadOnlyList<string>? allowedRoles, string? typeId, string? operation)
    {
        AllowedRoles = allowedRoles;
        Resource = typeId;
        Operation = operation;
    }

    // Factory for role check
    public static PermissionRequirement ForRoles(params string[] roles) =>
        new PermissionRequirement(roles, null, null);

    // Factory for model-operation check
    public static PermissionRequirement ForOperation(string resource, string operation) =>
        new PermissionRequirement(null, resource, operation);
}

/// <summary>
/// Factory for creating permission requirements, needed by Htmx.Components
/// </summary>
public class PermissionRequirementFactory : IPermissionRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
        => PermissionRequirement.ForOperation(resource, operation);

    public IAuthorizationRequirement ForRoles(params string[] roles)
        => PermissionRequirement.ForRoles(roles);
}
