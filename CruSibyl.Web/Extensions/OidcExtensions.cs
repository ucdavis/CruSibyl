using System.Security.Claims;
using CruSibyl.Core.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Serilog;

namespace CruSibyl.Web.Extensions;

public static class OidcExtensions
{
    public static void AddIamFallback(this OpenIdConnectOptions oidc)
    {
        oidc.Events.OnTicketReceived = async context =>
        {
            if (context.Principal == null || context.Principal.Identity == null)
            {
                return;
            }
            var identity = (ClaimsIdentity)context.Principal.Identity;

            // Sometimes CAS doesn't return the required IAM ID
            // If this happens, we take the reliable Kerberos (NameIdentifier claim) and use it to lookup IAM ID
            if (!identity.HasClaim(c => c.Type == UserService.IamIdClaimType) ||
                !identity.HasClaim(c => c.Type == ClaimTypes.Surname) ||
                !identity.HasClaim(c => c.Type == ClaimTypes.GivenName) ||
                !identity.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                var identityService = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
                var kerbId = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (kerbId != null)
                {
                    Log.Error($"CAS IAM Id Missing. For Kerb: {kerbId}");
                    var identityUser = await identityService.GetByKerberos(kerbId.Value);

                    if (identityUser != null)
                    {
                        if (!identity.HasClaim(c => c.Type == UserService.IamIdClaimType))
                        {
                            identity.AddClaim(new Claim(UserService.IamIdClaimType, identityUser.Iam));
                        }
                        //Check for other missing claims
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Surname))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Surname, identityUser.LastName));
                        }
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.GivenName))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.GivenName, identityUser.FirstName));
                        }
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Email))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Email, identityUser.Email));
                        }
                    }
                    else
                    {
                        Log.Error($"IAM Id Not Found with identity service. For Kerb: {kerbId}");
                    }
                }
                else
                {
                    Log.Error($"CAS IAM Id Missing. Kerb Not Found");
                }
            }

            // Ensure user exists in the db
            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            await userService.GetUser(identity.Claims.ToArray());
        };
    }
}