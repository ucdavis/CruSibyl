using Azure;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Htmx.Components.Table;
using Htmx;
using Htmx.Components;
using Htmx.Components.Models;
using Htmx.Components.ViewResults;
using Htmx.Components.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text.Json;
using Htmx.Components.Models.Table;
using Htmx.Components.Attributes;
using Htmx.Components.Models.Builders;
using Result = Htmx.Components.Models.Result;
using CruSibyl.Web.Models.Admin;
using CruSibyl.Core.Services;
using static Htmx.Components.State.PageStateConstants;

namespace CruSibyl.Web.Controllers;

[Authorize]
[Route("Admin")]
[NavActionGroup(DisplayName = "Admin", Icon = "fas fa-cogs", Order = 2)]
public class AdminController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IModelHandlerFactoryGeneric _modelHandlerFactory;


    public AdminController(AppDbContext dbContext, IModelHandlerFactoryGeneric modelHandlerFactory)
    {
        _dbContext = dbContext;
        _modelHandlerFactory = modelHandlerFactory;
    }

    [HttpGet("Repos")]
    [NavAction(DisplayName = "Repos", Icon = "fas fa-database", Order = 0, PushUrl = true, ViewName = "_Repos")]
    public async Task<IActionResult> Repos()
    {
        var modelHandler = await _modelHandlerFactory.Get<Repo, int>(nameof(Repo), ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModelAndFetchPage();

        return Ok(tableModel);
    }

    [HttpGet("AdminUsers")]
    [NavAction(DisplayName = "Admin Users", Icon = "fas fa-users-cog", Order = 1, PushUrl = true, ViewName = "_AdminUsers")]
    public async Task<IActionResult> AdminUsers()
    {
        var modelHandler = await _modelHandlerFactory.Get<AdminUserModel, int>(nameof(AdminUserModel), ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModelAndFetchPage();

        return Ok(tableModel);
    }

    [ModelConfig(nameof(AdminUserModel))]
    private void ConfigureAdminUser(ModelHandlerBuilder<AdminUserModel, int> builder)
    {
        builder
            .WithKeySelector(u => u.Id)
            .WithQueryable(() => _dbContext.Users
                .Where(u => u.Permissions.Any(p => p.Role.Name == Role.Codes.Admin || p.Role.Name == Role.Codes.System))
                .Select(u => new AdminUserModel
                {
                    Id = u.Id,
                    Name = u.FirstName + " " + u.LastName,
                    Email = u.Email,
                    Kerberos = u.Kerberos,
                    IsSystemAdmin = u.Permissions.Any(p => p.Role.Name == Role.Codes.System)
                }))
            .WithCreate(async adminUserModel =>
            {
                var userService = builder.ServiceProvider.GetRequiredService<IUserService>();
                User? user = null;
                if (!string.IsNullOrWhiteSpace(adminUserModel.Email))
                {
                    user = await userService.GetUser(adminUserModel.Email, includePermissions: true);
                }
                else if (!string.IsNullOrWhiteSpace(adminUserModel.Kerberos))
                {
                    user = await userService.GetUser(adminUserModel.Kerberos, includePermissions: true);
                }

                if (user == null)
                {
                    return Htmx.Components.Models.Result.Error("User not found or could not be created.");
                }

                var userUpdated = false;
                if (!user.Permissions.Any(p => p.Role.Name == Role.Codes.Admin))
                {
                    user.Permissions.Add(new Permission { Role = await _dbContext.Roles.FirstAsync(r => r.Name == Role.Codes.Admin) });
                    userUpdated = true;
                }
                if (adminUserModel.IsSystemAdmin)
                {
                    user.Permissions.Add(new Permission { Role = await _dbContext.Roles.FirstAsync(r => r.Name == Role.Codes.System) });
                    userUpdated = true;
                }
                if (userUpdated)
                {
                    _dbContext.Users.Update(user);
                    await _dbContext.SaveChangesAsync();
                }
                adminUserModel.Id = user.Id;
                adminUserModel.Name = user.Name;
                adminUserModel.Email = user.Email;
                adminUserModel.Kerberos = user.Kerberos;
                return Htmx.Components.Models.Result.Value(adminUserModel);
            })
            .WithDelete(async id =>
            {
                var user = await _dbContext.Users
                    .Include(u => u.Permissions)
                    .ThenInclude(p => p.Role)
                    .FirstOrDefaultAsync(u => u.Id == id);
                if (user != null)
                {
                    var permissionsToRemove = user.Permissions
                        .Where(p => p.Role.Name == Role.Codes.Admin || p.Role.Name == Role.Codes.System)
                        .ToList();
                    _dbContext.Permissions.RemoveRange(permissionsToRemove);
                    await _dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                }
                return Htmx.Components.Models.Result.Error("User not found");
            })
            .WithInput(u => u.Email, config => config
                .WithLabel("Email")
                .WithPlaceholder("Email to look up")
                .WithCssClass("form-control"))
            .WithInput(u => u.Kerberos, config => config
                .WithLabel("Kerberos")
                .WithPlaceholder("Kerberos to look up")
                .WithCssClass("form-control"))
            .WithInput(u => u.IsSystemAdmin, config => config
                .WithLabel("System Admin")
                .WithCssClass("form-check"))
            .WithTable(table => table
                .WithCrudActions()
                .AddSelectorColumn(x => x.Name)
                .AddSelectorColumn(x => x.Email, config => config.WithEditable())
                .AddSelectorColumn(x => x.Kerberos, config => config.WithEditable())
                .AddSelectorColumn(x => x.IsSystemAdmin, config => config.WithEditable())
                .AddCrudDisplayColumn());
    }



    [ModelConfig(nameof(Repo))]
    private void ConfigureRepo(ModelHandlerBuilder<Repo, int> builder)
    {
        builder
            .WithKeySelector(r => r.Id)
            .WithQueryable(() => _dbContext.Repos)
            .WithCreate(async repo =>
            {
                _dbContext.Repos.Add(repo);
                await _dbContext.SaveChangesAsync();
                return Htmx.Components.Models.Result.Value(repo);
            })
            .WithUpdate(async repo =>
            {
                _dbContext.Repos.Update(repo);
                await _dbContext.SaveChangesAsync();
                return Htmx.Components.Models.Result.Value(repo);
            })
            .WithDelete(async id =>
            {
                var repo = await _dbContext.Repos.FindAsync(id);
                if (repo != null)
                {
                    _dbContext.Repos.Remove(repo);
                    await _dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                }
                return Htmx.Components.Models.Result.Error("Repo not found");
            })
            .WithInput(r => r.Name, config => config
                .WithLabel("Name")
                .WithPlaceholder("Enter repo name")
                .WithCssClass("form-control"))
            .WithInput(r => r.Description, config => config
                .WithLabel("Description")
                .WithPlaceholder("Enter repo description")
                .WithCssClass("form-control"))
            .WithTable(table => table
                .WithCrudActions()
                .AddSelectorColumn(x => x.Name, config => config.WithEditable())
                .AddSelectorColumn(x => x.Description!, config => config.WithEditable())
                .AddCrudDisplayColumn());
    }
}
