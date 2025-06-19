using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.AuthStatus;

public class DefaultAuthStatusProvider : IAuthStatusProvider
{
    public Task<AuthStatusViewModel> GetAuthStatusAsync(ClaimsPrincipal user)
    {
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        return Task.FromResult(new AuthStatusViewModel
        {
            IsAuthenticated = isAuthenticated,
            UserName = isAuthenticated ? user.Identity?.Name : null,
            ProfileImageUrl = null, // No image by default
            LoginUrl = "/Auth/Login" // Default login route
        });
    }
}