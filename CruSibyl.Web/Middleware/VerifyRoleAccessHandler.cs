using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CruSibyl.Web.Middleware;

public class VerifyRoleAccessHandler : AuthorizationHandler<VerifyRoleAccess>
{
    private readonly AppDbContext _dbContext;

    private readonly IHttpContextAccessor _httpContext;

    public VerifyRoleAccessHandler(AppDbContext dbContext, IHttpContextAccessor httpContext)
    {
        _dbContext = dbContext;
        _httpContext = httpContext;
    }
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, VerifyRoleAccess requirement)
    {
        var userIamId = context.User.Claims.SingleOrDefault(c => c.Type == UserService.IamIdClaimType)?.Value;
        var kerbId = context.User.Claims.SingleOrDefault(a => a.Type == ClaimTypes.NameIdentifier)?.Value;


        if (string.IsNullOrWhiteSpace(userIamId))
        {
            return;
        }

        if (await _dbContext.Permissions.AnyAsync(p
            => p.User.Iam == userIamId
            && requirement.RoleStrings.Contains(p.Role.Name)))
        {
            context.Succeed(requirement);
            return;
        }
    }
}
