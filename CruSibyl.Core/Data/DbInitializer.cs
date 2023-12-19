using CruSibyl.Core.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CruSibyl.Core.Data;
public class DbInitializer
{
    private readonly AppDbContext _dbContext;

    public DbInitializer(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Initialize(bool recreateDb)
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


        //for(int i = 1; i <= 5; i++)
        //{
        //    var user = new User { Email = $"fake{i}@ucdavis.edu",
        //        FirstName = $"Fake{i}",
        //        LastName = "Fake",
        //        Kerberos = $"fake{i}",
        //        Iam = $"100000000{i}",
        //    };
        //    await CheckAndCreateUser(user);
        //}

        await _dbContext.SaveChangesAsync();

        await _dbContext.SaveChangesAsync();
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
