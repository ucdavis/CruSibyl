using System.Security.Claims;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Services;

namespace CruSibyl.Core.Extensions;
public static class UserExtensions
{
    public static Claim[] GetClaims(this User user)
    {
        return new[]
        {
                new Claim(ClaimTypes.NameIdentifier, user.Kerberos),
                new Claim(ClaimTypes.Name, user.Kerberos),
                new Claim(ClaimTypes.GivenName, user.FirstName),
                new Claim(ClaimTypes.Surname, user.LastName),
                new Claim("name", user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(UserService.IamIdClaimType, user.Iam),
            };
    }
}
