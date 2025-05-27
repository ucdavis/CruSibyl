using CruSibyl.Core.Domain;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using CruSibyl.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CruSibyl.Core.Models;
using Serilog;

namespace CruSibyl.Core.Services;
public interface IUserService
{
    Task<User?> GetUser(Claim[] userClaims);
    Task<User?> GetCurrentUser();
    Task<IEnumerable<Permission>> GetCurrentPermissionsAsync();
    string? GetCurrentUserId();
}

public class UserService : IUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IIdentityService _identityService;
    private readonly AppDbContext _dbContext;
    public const string IamIdClaimType = "ucdPersonIAMID";

    public UserService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor, IIdentityService identityService)
    {
        _httpContextAccessor = httpContextAccessor;
        _identityService = identityService;
        _dbContext = dbContext;
    }

    public string? GetCurrentUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(IamIdClaimType);
        return userId;
    }

    public async Task<User?> GetCurrentUser()
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            Log.Warning("No HttpContext found. Unable to retrieve or create User.");
            return null;
        }

        var userClaims = _httpContextAccessor.HttpContext.User.Claims.ToArray();

        return await GetUser(userClaims);
    }

    public async Task<IEnumerable<Permission>> GetCurrentPermissionsAsync()
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            Log.Warning("No HttpContext found. Unable to retrieve User permissions.");
            return [];
        }

        var iamId = _httpContextAccessor.HttpContext.User.Claims.Single(c => c.Type == IamIdClaimType).Value;
        var permissions = await _dbContext.Permissions
            .Include(p => p.Role)
            .Where(p => p.User.Iam == iamId)
            .ToArrayAsync();
        return permissions;
    }

    // Get any user based on their claims, creating if necessary
    public async Task<User?> GetUser(Claim[] userClaims)
    {
        string iamId = userClaims.Single(c => c.Type == IamIdClaimType).Value;

        var dbUser = await _dbContext.Users.SingleOrDefaultAsync(a => a.Iam == iamId);

        if (dbUser != null)
        {
            if (dbUser.MothraId == null)
            {
                var foundUser = await _identityService.GetByKerberos(dbUser.Kerberos);
                if (foundUser != null)
                {
                    dbUser.MothraId = foundUser.MothraId;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return dbUser; // already in the db, just return straight away
        }
        else
        {
            // not in the db yet, create new user and return
            var newUser = new User
            {
                FirstName = userClaims.Single(c => c.Type == ClaimTypes.GivenName).Value,
                LastName = userClaims.Single(c => c.Type == ClaimTypes.Surname).Value,
                Email = userClaims.Single(c => c.Type == ClaimTypes.Email).Value,
                Iam = iamId,
                Kerberos = userClaims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value
            };

            var foundUser = await _identityService.GetByKerberos(newUser.Kerberos);
            if (foundUser != null)
            {
                newUser.MothraId = foundUser.MothraId;
            }

            await _dbContext.Users.AddAsync(newUser);

            await _dbContext.SaveChangesAsync();

            return newUser;
        }
    }

}
