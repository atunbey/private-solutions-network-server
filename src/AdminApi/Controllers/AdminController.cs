using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;
using Shared.Contracts.Admin;

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
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Username and Email are required." });

        var user = new User
        {
            ExternalId = request.ExternalId?.Trim() ?? string.Empty,
            Username = request.Username.Trim(),
            Email = request.Email.Trim()
        };
        dbContext.Users.Add(user);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "user.create", Details = user.Username });
        await dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetUser), new { id = user.Id },
            new UserResponse(user.Id, user.ExternalId, user.Username, user.Email, user.CreatedUtc));
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var u = await dbContext.Users
            .Include(x => x.UserGroups).ThenInclude(ug => ug.Group)
            .Include(x => x.DeviceAssignments).ThenInclude(da => da.Device)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        return Ok(new UserDetailResponse(
            u.Id, u.ExternalId, u.Username, u.Email, u.CreatedUtc,
            u.UserGroups.Select(ug => new GroupSummary(ug.Group.Id, ug.Group.Name)).ToList(),
            u.DeviceAssignments.Select(da => new DeviceSummary(da.Device.Id, da.Device.DeviceUuid, da.Device.DisplayName)).ToList()));
    }

    [HttpPost("users/{userId:guid}/groups/{groupId:guid}")]
    public async Task<IActionResult> AssignGroup(Guid userId, Guid groupId)
    {
        var exists = await dbContext.UserGroups.AnyAsync(x => x.UserId == userId && x.GroupId == groupId);
        if (!exists)
        {
            dbContext.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
            dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "user.group.assign", Details = $"user={userId},group={groupId}" });
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("users/{userId:guid}/groups/{groupId:guid}")]
    public async Task<IActionResult> RemoveGroup(Guid userId, Guid groupId)
    {
        var row = await dbContext.UserGroups.SingleOrDefaultAsync(x => x.UserId == userId && x.GroupId == groupId);
        if (row is null) return NoContent();
        dbContext.UserGroups.Remove(row);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "user.group.remove", Details = $"user={userId},group={groupId}" });
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("groups/{groupId:guid}/apps/{applicationId:guid}")]
    public async Task<IActionResult> AssignApplication(Guid groupId, Guid applicationId)
    {
        var exists = await dbContext.GroupApplications.AnyAsync(x => x.GroupId == groupId && x.ApplicationId == applicationId);
        if (!exists)
        {
            dbContext.GroupApplications.Add(new GroupApplication { GroupId = groupId, ApplicationId = applicationId });
            dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "group.app.assign", Details = $"group={groupId},app={applicationId}" });
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("groups/{groupId:guid}/apps/{applicationId:guid}")]
    public async Task<IActionResult> RemoveApplication(Guid groupId, Guid applicationId)
    {
        var row = await dbContext.GroupApplications.SingleOrDefaultAsync(x => x.GroupId == groupId && x.ApplicationId == applicationId);
        if (row is null) return NoContent();
        dbContext.GroupApplications.Remove(row);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "group.app.remove", Details = $"group={groupId},app={applicationId}" });
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    private string ActorName() => User.Identity?.Name ?? "system";
}
