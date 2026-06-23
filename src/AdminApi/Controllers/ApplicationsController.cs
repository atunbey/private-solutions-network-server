using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;
using Shared.Contracts.Admin;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin/apps")]
[Authorize]
public class ApplicationsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetApplications()
    {
        var apps = await dbContext.Applications
            .Select(a => new ApplicationResponse(a.Id, a.Name, a.BalenaAppSlug, a.Enabled, a.ServerAuthoritative))
            .ToListAsync();
        return Ok(apps);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetApplication(Guid id)
    {
        var a = await dbContext.Applications.FindAsync(id);
        if (a is null) return NotFound();
        return Ok(new ApplicationResponse(a.Id, a.Name, a.BalenaAppSlug, a.Enabled, a.ServerAuthoritative));
    }

    [HttpPost]
    public async Task<IActionResult> CreateApplication([FromBody] CreateApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BalenaAppSlug))
            return BadRequest(new { message = "Name and BalenaAppSlug are required." });

        if (await dbContext.Applications.AnyAsync(a => a.Name == request.Name.Trim()))
            return Conflict(new { message = "An application with that name already exists." });

        var app = new Application
        {
            Name = request.Name.Trim(),
            BalenaAppSlug = request.BalenaAppSlug.Trim(),
            ServerAuthoritative = request.ServerAuthoritative
        };
        dbContext.Applications.Add(app);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "app.create", Details = app.Name });
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetApplication), new { id = app.Id },
            new ApplicationResponse(app.Id, app.Name, app.BalenaAppSlug, app.Enabled, app.ServerAuthoritative));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateApplication(Guid id, [FromBody] UpdateApplicationRequest request)
    {
        var app = await dbContext.Applications.FindAsync(id);
        if (app is null) return NotFound();

        app.Name = request.Name.Trim();
        app.BalenaAppSlug = request.BalenaAppSlug.Trim();
        app.Enabled = request.Enabled;
        app.ServerAuthoritative = request.ServerAuthoritative;
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "app.update", Details = id.ToString() });
        await dbContext.SaveChangesAsync();

        return Ok(new ApplicationResponse(app.Id, app.Name, app.BalenaAppSlug, app.Enabled, app.ServerAuthoritative));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id)
    {
        var app = await dbContext.Applications
            .Include(x => x.GroupApplications)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (app is null) return NotFound();

        dbContext.GroupApplications.RemoveRange(app.GroupApplications);
        dbContext.Applications.Remove(app);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "app.delete", Details = id.ToString() });
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private string ActorName() => User.Identity?.Name ?? "system";
}
