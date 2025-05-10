using CruSibyl.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Data;
public class DbInitializer
{
    private readonly AppDbContext _dbContext;

    public DbInitializer(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeUsers(bool recreateDb)
    {
        if (recreateDb)
        {
            //do what needs to be done?
        }

        var JasonUser = await CheckAndCreateUser(new User
        {
            Email = "jsylvestre@ucdavis.edu",
            Kerberos = "jsylvest",
            FirstName = "Jason",
            LastName = "Sylvestre",
            Iam = "1000009309",
            MothraId = "00600825",
        });
        var ScottUser = await CheckAndCreateUser(new User
        {
            Email = "srkirkland@ucdavis.edu",
            Kerberos = "postit",
            FirstName = "Scott",
            LastName = "Kirkland",
            Iam = "1000029584",
            MothraId = "00183873",
        });
        var RiverUser = await CheckAndCreateUser(new User
        {
            Email = "laholstege@ucdavis.edu",
            Kerberos = "holstege",
            FirstName = "River",
            LastName = "Holstege",
            Iam = "1000243041",
            MothraId = "01224288",
        });

        var SpruceUser = await CheckAndCreateUser(new User
        {
            Email = "swebermilne@ucdavis.edu",
            Kerberos = "sweber",
            FirstName = "Spruce",
            LastName = "Weber-Milne",
            Iam = "1000255034",
            MothraId = "01259393",
        });
        var RobUser = await CheckAndCreateUser(new User
        {
            Email = "rmartinsen@ucdavis.edu",
            Kerberos = "rmartins",
            FirstName = "Robert",
            LastName = "Martinsen",
            Iam = "1000571302",
            MothraId = "00183346",
        });

        await CheckAndCreateRoles();

        var systemRole = await _dbContext.Roles.SingleAsync(a => a.Name == Role.Codes.System);

        await CheckAndCreatePermission(JasonUser, systemRole);
        await CheckAndCreatePermission(ScottUser, systemRole);
        await CheckAndCreatePermission(RiverUser, systemRole);
        await CheckAndCreatePermission(SpruceUser, systemRole);
        await CheckAndCreatePermission(RobUser, systemRole);

        await _dbContext.SaveChangesAsync();

    }

    public async Task CheckAndCreateRoles()
    {
        var systemRole = await CheckAndCreateRole(Role.Codes.System);
        var adminRole = await CheckAndCreateRole(Role.Codes.Admin);
    }

    private async Task<Permission> CheckAndCreatePermission(User user, Role role)
    {
        var permissionToCreate = await _dbContext.Permissions.SingleOrDefaultAsync(a => a.User == user && a.Role == role);
        if (permissionToCreate == null)
        {
            permissionToCreate = new Permission
            {
                User = user,
                Role = role,
            };
            await _dbContext.Permissions.AddAsync(permissionToCreate);
        }
        return permissionToCreate;
    }

    private async Task<Role> CheckAndCreateRole(string roleName)
    {
        var roleToCreate = await _dbContext.Roles.SingleOrDefaultAsync(a => a.Name == roleName);
        if (roleToCreate == null)
        {
            roleToCreate = new Role
            {
                Name = roleName,
            };
            await _dbContext.Roles.AddAsync(roleToCreate);
        }
        return roleToCreate;
    }

    private async Task<User> CheckAndCreateUser(User user)
    {
        var userToCreate = await _dbContext.Users.SingleOrDefaultAsync(a => a.Iam == user.Iam);
        if (userToCreate == null)
        {
            userToCreate = user;
            await _dbContext.Users.AddAsync(userToCreate);
        }
        return userToCreate;
    }
}
