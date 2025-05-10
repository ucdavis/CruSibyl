using CruSibyl.Core.Domain;

namespace CruSibyl.Core.Models;

public static class AccessPolicies
{
    public const string SystemAccess = "SystemAccess";
    public const string AdminAccess = "AdminAccess";


    public static string[] GetRoles(string accessPolicy)
    {
        return accessPolicy switch
        {
            SystemAccess => [Role.Codes.System],
            AdminAccess => [Role.Codes.System, Role.Codes.Admin],
            _ => throw new ArgumentException("Invalid access policy", nameof(accessPolicy))
        };
    }
}

