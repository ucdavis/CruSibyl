using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Htmx.Components.Services;

public interface IAuthorizationMetadataService
{
    Task<AuthorizationMetadata> GetMetadataAsync(ControllerActionDescriptor descriptor);
    Task<bool> IsAuthorizedAsync(ControllerActionDescriptor descriptor, ClaimsPrincipal user);
}

public class AuthorizationMetadata
{
    public string[] Policies { get; set; } = [];
    public string[] Roles { get; set; } = [];
    public bool OnlyRequiresAuthentication { get; set; }
    public bool AllowAnonymous { get; set; }
}

public class AuthorizationMetadataSettings
{
    public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;
}

public class AuthorizationMetadataService : IAuthorizationMetadataService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IMemoryCache _cache;
    private readonly AuthorizationMetadataSettings _settings;
    private readonly IRoleService? _roleService;
    private const int MetadataCacheExpirationMinutes = 10;
    private const int AuthorizationCacheExpirationMinutes = 2;

    public AuthorizationMetadataService(
        IAuthorizationService authorizationService,
        IMemoryCache cache,
        IOptions<AuthorizationMetadataSettings> settings,
        IRoleService? roleService = null)
    {
        _authorizationService = authorizationService;
        _cache = cache;
        _settings = settings.Value ?? new AuthorizationMetadataSettings();
        _roleService = roleService;
    }

    public async Task<AuthorizationMetadata> GetMetadataAsync(ControllerActionDescriptor descriptor)
    {
        // Extract and cache attribute metadata (not user-specific)
        var result = await _cache.GetOrCreateAsync(
            $"authmeta:{descriptor.UniqueId()}",
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(MetadataCacheExpirationMinutes);
                var authorizeAttrs = descriptor.MethodInfo
                    .GetCustomAttributes<AuthorizeAttribute>(true)
                    .Concat(descriptor.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>(true))
                    .ToArray();

                var policies = authorizeAttrs
                    .Where(attr => !string.IsNullOrEmpty(attr.Policy))
                    .Select(attr => attr.Policy!)
                    .Distinct()
                    .ToArray();

                var roles = authorizeAttrs
                    .Where(attr => !string.IsNullOrEmpty(attr.Roles))
                    .SelectMany(attr => attr.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct()
                    .ToArray();

                var onlyRequiresAuthentication = authorizeAttrs.Length > 0
                    && !policies.Any()
                    && !roles.Any();

                var allowAnonymous = descriptor.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null
                    || descriptor.ControllerTypeInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null;

                // Always return a non-null AuthorizationMetadata
                return Task.FromResult(new AuthorizationMetadata
                {
                    Policies = policies,
                    Roles = roles,
                    OnlyRequiresAuthentication = onlyRequiresAuthentication,
                    AllowAnonymous = allowAnonymous
                });
            });
        return result!;
    }

    public async Task<bool> IsAuthorizedAsync(ControllerActionDescriptor descriptor, ClaimsPrincipal user)
    {
        var meta = await GetMetadataAsync(descriptor);

        // AllowAnonymous always wins
        if (meta.AllowAnonymous)
            return true;

        // If there are no [Authorize] attributes at all, treat as public
        if (!meta.OnlyRequiresAuthentication && meta.Policies.Length == 0 && meta.Roles.Length == 0)
            return true;

        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

        // If only authentication is required
        if (meta.OnlyRequiresAuthentication)
            return isAuthenticated;

        // If not authenticated, fail fast (unless AllowAnonymous, already handled above)
        if (!isAuthenticated)
            return false;

        // Require ALL policies to succeed (AND semantics)
        foreach (var policy in meta.Policies)
        {
            var userId = user.FindFirst(_settings.UserIdClaimType)?.Value ?? "anonymous";
            var isAuthorized = await _cache.GetOrCreateAsync(
                $"authz:{userId}:{policy}",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AuthorizationCacheExpirationMinutes);
                    var authResult = await _authorizationService.AuthorizeAsync(user, policy);
                    return authResult.Succeeded;
                });
            if (!isAuthorized)
                return false;
        }

        // Roles: OR semantics (any role is sufficient)
        if (meta.Roles.Length > 0)
        {
            if (_roleService == null)
                throw new InvalidOperationException("Role-based authorization is required, but no IRoleService is registered.");

            var hasRole = await _roleService.UserHasAnyRoleAsync(user, meta.Roles);
            if (!hasRole)
                return false;
        }

        // If we reach here, all requirements are satisfied
        return true;
    }
}


// Helper for unique descriptor keying
public static class DescriptorExtensions
{
    public static string UniqueId(this ControllerActionDescriptor desc)
        => $"{desc.ControllerTypeInfo.FullName}.{desc.ActionName}";
}