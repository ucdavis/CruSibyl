using System.Security.Claims;

namespace Htmx.Components.AuthStatus;

public interface IAuthStatusProvider
{
    Task<AuthStatusViewModel> GetAuthStatusAsync(ClaimsPrincipal user);
}