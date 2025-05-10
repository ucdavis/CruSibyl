using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CruSibyl.Web.Middleware;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _dbContext;

    public PermissionHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userIamId = context.User.Claims.SingleOrDefault(c => c.Type == "ucdPersonIAMID")?.Value;

        if (string.IsNullOrWhiteSpace(userIamId))
            return;

        // Check if the user is a system admin
        var isSystemAdmin = await _dbContext.Permissions
            .Include(p => p.Role)
            .AnyAsync(p =>
                p.User.Iam == userIamId &&
                p.Role.Name == Role.Codes.System);
        if (isSystemAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        // Simple role check
        if (requirement.AllowedRoles != null && requirement.AllowedRoles.Count > 0)
        {
            var hasRole = await _dbContext.Permissions
                .Include(p => p.Role)
                .AnyAsync(p =>
                    p.User.Iam == userIamId &&
                    requirement.AllowedRoles.Contains(p.Role.Name));
            if (hasRole)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // RBAC check (Resource + Operation)
        if (!string.IsNullOrEmpty(requirement.Resource) && !string.IsNullOrEmpty(requirement.Operation))
        {
            // Get all roles for this user
            var hasRoleWithResourceAccess = await _dbContext.Permissions
                .Where(p => p.User.Iam == userIamId
                    && p.Role.Operations
                        .Any(o => o.Resource == requirement.Resource && o.Operation == requirement.Operation))
                .AnyAsync();
            if (hasRoleWithResourceAccess)
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}