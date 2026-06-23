using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;
using Shared.Contracts.Admin;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin/groups")]
[Authorize]
public class GroupsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await dbContext.Groups
            .Select(g => new GroupResponse(g.Id, g.Name))
            .ToListAsync();
        return Ok(groups);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGroup(Guid id)
    {
        var g = await dbContext.Groups
            .Include(x => x.UserGroups).ThenInclude(ug => ug.User)
            .Include(x => x.GroupApplications).ThenInclude(ga => ga.Application)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (g is null) return NotFound();

        return Ok(new GroupDetailResponse(
            g.Id,
            g.Name,
            g.UserGroups.Select(ug => new UserSummary(ug.User.Id, ug.User.Username, ug.User.Email)).ToList(),
            g.GroupApplications.Select(ga => new ApplicationSummary(
                ga.Application.Id, ga.Application.Name, ga.Application.BalenaAppSlug, ga.Application.Enabled)).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        if (await dbContext.Groups.AnyAsync(g => g.Name == request.Name.Trim()))
            return Conflict(new { message = "A group with that name already exists." });

        var group = new Group { Name = request.Name.Trim() };
        dbContext.Groups.Add(group);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "group.create", Details = group.Name });
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, new GroupResponse(group.Id, group.Name));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        var group = await dbContext.Groups
            .Include(x => x.UserGroups)
            .Include(x => x.GroupApplications)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (group is null) return NotFound();

        dbContext.UserGroups.RemoveRange(group.UserGroups);
        dbContext.GroupApplications.RemoveRange(group.GroupApplications);
        dbContext.Groups.Remove(group);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "group.delete", Details = id.ToString() });
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private string ActorName() => User.Identity?.Name ?? "system";
}
