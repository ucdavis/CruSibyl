namespace Htmx.Components.Authorization;

/// <summary>
/// Ensures that given resource and operation are registered with the authorization system.
/// </summary>
public interface IResourceOperationRegistry
{
    Task Register(string resource, string operation);
}
