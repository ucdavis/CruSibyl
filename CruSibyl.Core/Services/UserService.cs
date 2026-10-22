using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Services;

public interface IUserService
{
    Task<User?> GetUserAsync(Claim[] userClaims, bool includePermissions = false);
    Task<User?> GetUser(string iamIdOrEmail, bool includePermissions = false);
    Task<User?> GetCurrentUserAsync(bool includePermissions = false);
    Task<IEnumerable<Permission>> GetCurrentPermissionsAsync();
    string? GetCurrentUserId();
}

public class UserService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor, IIdentityService identityService) : IUserService
{
    public const string IamIdClaimType = "ucdPersonIAMID";

    public static string RequireIamId(IEnumerable<Claim> claims)
    {
        var ids = claims.Where(c => c.Type == IamIdClaimType).Select(c => c.Value).ToArray();
        if (ids.Length != 1 || string.IsNullOrWhiteSpace(ids[0]) || ids[0].Length > 10 || !ids[0].All(char.IsAsciiDigit))
            throw new IdentityResolutionException("Sign-in requires one valid IAM ID claim.");
        return ids[0];
    }

    public string? GetCurrentUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true ? RequireIamId(principal.Claims) : null;
    }

    public async Task<User?> GetCurrentUserAsync(bool includePermissions = false)
    {
        var iam = GetCurrentUserId();
        return iam == null ? null : await FindUser(iam, includePermissions);
    }

    public async Task<IEnumerable<Permission>> GetCurrentPermissionsAsync()
    {
        var iam = GetCurrentUserId();
        if (iam == null) return [];
        return await dbContext.Permissions.Include(p => p.Role).Where(p => p.User.Iam == iam).ToArrayAsync();
    }

    private Task<User?> FindUser(string iam, bool includePermissions)
    {
        IQueryable<User> users = dbContext.Users;
        if (includePermissions) users = users.Include(u => u.Permissions).ThenInclude(p => p.Role);
        return users.SingleOrDefaultAsync(u => u.Iam == iam);
    }

    private async Task<User?> FindOrCreate(User? person, string? expectedIam, bool includePermissions)
    {
        if (person == null) return null;
        RequireIamId([new Claim(IamIdClaimType, person.Iam)]);
        if (expectedIam != null && person.Iam != expectedIam)
            throw new IdentityResolutionException("Directory identity does not match the signed-in user.");
        var existing = await FindUser(person.Iam, includePermissions);
        if (existing != null) return existing;
        // A shared email is not permission to merge two IAM identities.
        if (await dbContext.Users.AnyAsync(u => u.Email == person.Email && u.Iam != person.Iam))
            throw new IdentityResolutionException("Email is associated with a different IAM identity.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(person, new ValidationContext(person), errors, true))
            throw new IdentityResolutionException("Directory profile is incomplete or invalid.");
        person.Permissions.Clear();
        dbContext.Users.Add(person);
        await dbContext.SaveChangesAsync();
        return person;
    }

    public async Task<User?> GetUserAsync(Claim[] userClaims, bool includePermissions = false)
    {
        var iam = RequireIamId(userClaims);
        var existing = await FindUser(iam, includePermissions);
        if (existing != null) return existing;
        return await FindOrCreate(await identityService.GetByIamId(iam), iam, includePermissions);
    }

    public async Task<User?> GetUser(string iamIdOrEmail, bool includePermissions = false)
    {
        var query = iamIdOrEmail?.Trim();
        if (string.IsNullOrEmpty(query)) return null;
        if (query.All(char.IsAsciiDigit))
        {
            RequireIamId([new Claim(IamIdClaimType, query)]);
            var existing = await FindUser(query, includePermissions);
            if (existing != null) return existing;
            return await FindOrCreate(await identityService.GetByIamId(query), query, includePermissions);
        }
        if (!new EmailAddressAttribute().IsValid(query)) return null;
        return await FindOrCreate(await identityService.GetByEmail(query), null, includePermissions);
    }
}
