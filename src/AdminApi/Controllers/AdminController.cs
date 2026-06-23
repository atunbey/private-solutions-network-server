using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await dbContext.Users
            .Select(u => new { u.Id, u.ExternalId, u.Username, u.Email, u.CreatedUtc })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] User user)
    {
        dbContext.Users.Add(user);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Actor = User.Identity?.Name ?? "system",
            Action = "user.create",
            Details = user.Username
        });
        await dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
    }

    [HttpPost("users/{userId:guid}/groups/{groupId:guid}")]
    public async Task<IActionResult> AssignGroup(Guid userId, Guid groupId)
    {
        var exists = await dbContext.UserGroups.AnyAsync(x => x.UserId == userId && x.GroupId == groupId);
        if (!exists)
        {
            dbContext.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
            dbContext.AuditLogs.Add(new AuditLog
            {
                Actor = User.Identity?.Name ?? "system",
                Action = "user.group.assign",
                Details = $"user={userId},group={groupId}"
            });
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPost("groups/{groupId:guid}/apps/{applicationId:guid}")]
    public async Task<IActionResult> AssignApplication(Guid groupId, Guid applicationId)
    {
        var exists = await dbContext.GroupApplications.AnyAsync(x => x.GroupId == groupId && x.ApplicationId == applicationId);
        if (!exists)
        {
            dbContext.GroupApplications.Add(new GroupApplication { GroupId = groupId, ApplicationId = applicationId });
            dbContext.AuditLogs.Add(new AuditLog
            {
                Actor = User.Identity?.Name ?? "system",
                Action = "group.app.assign",
                Details = $"group={groupId},app={applicationId}"
            });
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }
}
