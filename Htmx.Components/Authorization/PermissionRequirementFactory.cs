using Microsoft.AspNetCore.Authorization;

namespace Htmx.Components.Authorization;

public interface IPermissionRequirementFactory
{
    IAuthorizationRequirement ForOperation(string resource, string operation);
    IAuthorizationRequirement ForRoles(params string[] roles);
}

public class DefaultPermssionRequirementFactory : IPermissionRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
    {
        throw new NotImplementedException($"Please register an implementation of {nameof(IAuthorizationRequirement)} as a singleton");
    }

    public IAuthorizationRequirement ForRoles(params string[] roles)
    {
        throw new NotImplementedException($"Please register an implementation of {nameof(IAuthorizationRequirement)} as a singleton");
    }
}