using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Shared.Contracts.Policy;

namespace PolicyApi.Controllers;

[ApiController]
[Route("api/policy")]
[Authorize]
public class PolicyController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("apps")]
    public async Task<ActionResult<PolicyResponseDto>> GetAppsForCurrentUser()
    {
        var externalId = User.FindFirstValue("sub") ?? string.Empty;
        var username = User.FindFirstValue("preferred_username") ?? User.Identity?.Name ?? string.Empty;

        var user = await dbContext.Users
            .Include(x => x.UserGroups)
            .ThenInclude(ug => ug.Group)
            .SingleOrDefaultAsync(x => x.ExternalId == externalId || x.Username == username);

        if (user is null)
        {
            return NotFound(new { message = "User not mapped in platform database." });
        }

        var groupIds = user.UserGroups.Select(g => g.GroupId).ToArray();

        var apps = await dbContext.GroupApplications
            .Where(ga => groupIds.Contains(ga.GroupId) && ga.Application.Enabled)
            .Select(ga => new AppDto(ga.Application.Id, ga.Application.Name, ga.Application.BalenaAppSlug))
            .Distinct()
            .ToListAsync();

        var groups = user.UserGroups.Select(g => g.Group.Name).Distinct().ToList();

        return Ok(new PolicyResponseDto(user.Username, groups, apps));
    }
}
