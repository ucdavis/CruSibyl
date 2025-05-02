using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Htmx.Components;

public static class OidcOptionsExtensions
{
    public static void ConfigureHtmxAuthPopup(this OpenIdConnectOptions oidc, string url)
    {
        oidc.Events.OnRedirectToIdentityProvider = context =>
        {
            if (context.Request.IsHtmx())
            {
                // We need to tell HTMX to redirect to the login page and skip the default redirect
                context.Response.StatusCode = 401;
                context.Response.Headers["X-Auth-Failure"] = $"popup-login:{url}";
                context.HandleResponse();
            }
            return Task.CompletedTask;
        };
        oidc.Events.OnAccessDenied = context =>
        {
            if (context.Request.IsHtmx())
            {
                // We need to tell HTMX that auth was denied and skip the default redirect
                context.Response.StatusCode = 403;
                context.Response.Headers["HX-Trigger"] = "auth-denied";
                context.HandleResponse();
            }
            return Task.CompletedTask;
        };
    }
}