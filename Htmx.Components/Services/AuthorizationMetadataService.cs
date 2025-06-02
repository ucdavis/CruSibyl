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

    public AuthorizationMetadataService(
        IAuthorizationService authorizationService,
        IMemoryCache cache,
        IOptions<AuthorizationMetadataSettings> settings)
    {
        _authorizationService = authorizationService;
        _cache = cache;
        _settings = settings.Value ?? new AuthorizationMetadataSettings();
    }

    public Task<AuthorizationMetadata> GetMetadataAsync(ControllerActionDescriptor descriptor)
    {
        // // Extract and cache attribute metadata (not user-specific)
        // var result = await _cache.GetOrCreateAsync(
        //     $"authmeta:{descriptor.UniqueId()}",
        //     entry =>
        //     {
        //         entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
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

                var onlyRequiresAuthentication = authorizeAttrs.Any(attr =>
                    string.IsNullOrEmpty(attr.Policy) && string.IsNullOrEmpty(attr.Roles)
                );

                var allowAnonymous = descriptor.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null
                    || descriptor.ControllerTypeInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null
                    || (!policies.Any() && !roles.Any() && !onlyRequiresAuthentication);

                // Always return a non-null AuthorizationMetadata
                return Task.FromResult(new AuthorizationMetadata
                {
                    Policies = policies,
                    Roles = roles,
                    OnlyRequiresAuthentication = onlyRequiresAuthentication,
                    AllowAnonymous = allowAnonymous
                });
        //     });
        // return result!;
    }

    public async Task<bool> IsAuthorizedAsync(ControllerActionDescriptor descriptor, ClaimsPrincipal user)
    {
        var meta = await GetMetadataAsync(descriptor);
        if (meta.AllowAnonymous)
            return true;

        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

        if (meta.OnlyRequiresAuthentication && isAuthenticated)
            return true;

        foreach (var policy in meta.Policies)
        {
            // var userId = user.FindFirst(_settings.UserIdClaimType)?.Value ?? "anonymous";
            // var result = await _cache.GetOrCreateAsync(
            //     $"authz:{userId}:{policy}",
            //     async entry =>
            //     {
            //         entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                    var authResult = await _authorizationService.AuthorizeAsync(user, policy);
                    return authResult.Succeeded;
            //     });
            // if (result is bool b && b)
            //     return true;
        }

        var userRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet();

        if (meta.Roles.Any(role => userRoles.Contains(role)))
            return true;

        return false;
    }
}

// Helper for unique descriptor keying
public static class DescriptorExtensions
{
    public static string UniqueId(this ControllerActionDescriptor desc)
        => $"{desc.ControllerTypeInfo.FullName}.{desc.ActionName}";
}