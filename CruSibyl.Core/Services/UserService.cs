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
    Task<User?> GetUser(Claim[] userClaims, bool includePermissions = false);
    Task<User?> GetUser(string iamIdOrEmailOrKerberos, bool includePermissions = false);
    Task<User?> GetCurrentUser(bool includePermissions = false);
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

    public async Task<User?> GetCurrentUser(bool includePermissions = false)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            Log.Warning("No HttpContext found. Unable to retrieve or create User.");
            return null;
        }

        var userClaims = _httpContextAccessor.HttpContext.User.Claims.ToArray();

        return await GetUser(userClaims, includePermissions);
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

    // Shared helper for finding or creating a user in the DB
    private async Task<User?> FindOrCreateUserAsync(User identityUser, bool includePermissions)
    {
        User? dbUser = null;

        if (!string.IsNullOrWhiteSpace(identityUser.Iam))
            dbUser = includePermissions
            ? await _dbContext.Users.Include(u => u.Permissions).ThenInclude(p => p.Role)
                .SingleOrDefaultAsync(a => a.Iam == identityUser.Iam)
            : await _dbContext.Users.SingleOrDefaultAsync(a => a.Iam == identityUser.Iam);

        if (dbUser == null && !string.IsNullOrWhiteSpace(identityUser.Kerberos))
            dbUser = includePermissions
            ? await _dbContext.Users.Include(u => u.Permissions).ThenInclude(p => p.Role)
                .SingleOrDefaultAsync(a => a.Kerberos == identityUser.Kerberos)
            : await _dbContext.Users.SingleOrDefaultAsync(a => a.Kerberos == identityUser.Kerberos);

        if (dbUser == null && !string.IsNullOrWhiteSpace(identityUser.Email))
            dbUser = includePermissions
            ? await _dbContext.Users.Include(u => u.Permissions).ThenInclude(p => p.Role)
                .SingleOrDefaultAsync(a => a.Email == identityUser.Email)
            : await _dbContext.Users.SingleOrDefaultAsync(a => a.Email == identityUser.Email);

        if (dbUser != null)
        {
            if (string.IsNullOrWhiteSpace(dbUser.MothraId) && !string.IsNullOrWhiteSpace(identityUser.MothraId))
            {
                dbUser.MothraId = identityUser.MothraId;
                await _dbContext.SaveChangesAsync();
            }
            return dbUser;
        }
        else
        {
            await _dbContext.Users.AddAsync(identityUser);
            await _dbContext.SaveChangesAsync();
            return identityUser;
        }
    }

    public async Task<User?> GetUser(Claim[] userClaims, bool includePermissions = false)
    {
        string iamId = userClaims.Single(c => c.Type == IamIdClaimType).Value;
        string firstName = userClaims.Single(c => c.Type == ClaimTypes.GivenName).Value;
        string lastName = userClaims.Single(c => c.Type == ClaimTypes.Surname).Value;
        string email = userClaims.Single(c => c.Type == ClaimTypes.Email).Value;
        string kerberos = userClaims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value;

        // Try to get more info from identity service
        User? identityUser = await _identityService.GetByKerberos(kerberos);

        if (identityUser == null)
        {
            // Fallback to claims if identity service fails
            identityUser = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Iam = iamId,
                Kerberos = kerberos
            };
        }
        else
        {
            // Ensure IAM, email, etc. are set from claims if missing
            if (string.IsNullOrWhiteSpace(identityUser.Iam)) identityUser.Iam = iamId;
            if (string.IsNullOrWhiteSpace(identityUser.Email)) identityUser.Email = email;
            if (string.IsNullOrWhiteSpace(identityUser.FirstName)) identityUser.FirstName = firstName;
            if (string.IsNullOrWhiteSpace(identityUser.LastName)) identityUser.LastName = lastName;
            if (string.IsNullOrWhiteSpace(identityUser.Kerberos)) identityUser.Kerberos = kerberos;
        }

        return await FindOrCreateUserAsync(identityUser, includePermissions);
    }

    public async Task<User?> GetUser(string iamIdOrEmailOrKerberos, bool includePermissions = false)
    {
        if (string.IsNullOrWhiteSpace(iamIdOrEmailOrKerberos))
            return null;

        User? identityUser = null;

        if (iamIdOrEmailOrKerberos.All(char.IsDigit))
        {
            // IAM ID: use GetByKerberos (since IdentityService doesn't have GetByIamId)
            identityUser = await _identityService.GetByKerberos(iamIdOrEmailOrKerberos);
        }
        else if (iamIdOrEmailOrKerberos.Contains('@'))
        {
            identityUser = await _identityService.GetByEmail(iamIdOrEmailOrKerberos);
        }
        else
        {
            identityUser = await _identityService.GetByKerberos(iamIdOrEmailOrKerberos);
        }

        if (identityUser == null)
            return null;

        return await FindOrCreateUserAsync(identityUser, includePermissions);
    }
}
