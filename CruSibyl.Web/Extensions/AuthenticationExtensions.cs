using CruSibyl.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace CruSibyl.Web.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddEntraAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var key in new[] { "TenantId", "ClientId", "ClientSecret", "IamKey" })
        {
            var value = configuration[$"Auth:{key}"];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('<') || value.StartsWith('['))
                throw new InvalidOperationException($"Auth:{key} must be configured for Entra/IAM sign-in.");
        }
        if (!Guid.TryParse(configuration["Auth:TenantId"], out _))
            throw new InvalidOperationException("Auth:TenantId must identify one tenant.");

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        }).AddMicrosoftIdentityWebApp(configuration.GetSection("Auth"));

        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.ResponseType = "code";
            options.UsePkce = true;
            options.TokenValidationParameters.NameClaimType = "name";
            var redirect = options.Events.OnRedirectToIdentityProvider;
            var denied = options.Events.OnAccessDenied;
            var validated = options.Events.OnTokenValidated;
            var failed = options.Events.OnRemoteFailure;
            options.Events.OnRedirectToIdentityProvider = async context =>
            {
                if (IsHtmx(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers["X-Auth-Failure"] = "popup-login:/auth/popup-login";
                    context.HandleResponse();
                    return;
                }
                context.ProtocolMessage.DomainHint = "ucdavis.edu";
                await redirect(context);
            };
            options.Events.OnAccessDenied = async context =>
            {
                if (IsHtmx(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.Headers["HX-Trigger"] = "auth-denied";
                    context.HandleResponse();
                    return;
                }
                await denied(context);
            };
            options.Events.OnTokenValidated = async context =>
            {
                await validated(context);
                if (context.Result != null) return;
                try
                {
                    var principal = context.Principal ?? throw new IdentityResolutionException("Missing sign-in identity.");
                    UserService.RequireIamId(principal.Claims);
                    var users = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    var user = await users.GetUserAsync(principal.Claims.ToArray());
                    if (user == null) context.Fail("IAM profile could not be resolved.");
                }
                catch (Exception exception)
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("EntraSignIn").LogWarning("IAM sign-in could not be completed ({FailureType}).", exception.GetType().Name);
                    context.Fail("IAM sign-in could not be completed.");
                }
            };
            options.Events.OnRemoteFailure = async context =>
            {
                await failed(context);
                if (context.Result != null) return;
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Sign-in could not be completed. Please contact the application administrator.");
            };
        });

        services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = ".CruSibyl.Entra";
            var denied = options.Events.OnRedirectToAccessDenied;
            options.Events.OnRedirectToAccessDenied = async context =>
            {
                if (IsHtmx(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.Headers["HX-Trigger"] = "auth-denied";
                    return;
                }
                await denied(context);
            };
        });
        return services;
    }

    private static bool IsHtmx(HttpRequest request) =>
        string.Equals(request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
}
