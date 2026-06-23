using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;
using Shared.Contracts.Policy;

namespace PolicyApi.Controllers;

[ApiController]
[Route("api/policy/node-backups")]
[Authorize]
public class NodeBackupController(AppDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProgressBackupResponse>> CreateBackup([FromBody] ProgressBackupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NodeId) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.ApplicationName))
        {
            return BadRequest(new { message = "NodeId, Username and ApplicationName are required." });
        }

        var backup = new ProgressBackup
        {
            NodeId = request.NodeId.Trim(),
            Username = request.Username.Trim(),
            ApplicationName = request.ApplicationName.Trim(),
            ProgressJson = string.IsNullOrWhiteSpace(request.ProgressJson) ? "{}" : request.ProgressJson,
            CapturedUtc = request.CapturedUtc ?? DateTime.UtcNow
        };

        dbContext.ProgressBackups.Add(backup);
        await dbContext.SaveChangesAsync();

        return Ok(new ProgressBackupResponse(
            backup.Id,
            backup.NodeId,
            backup.Username,
            backup.ApplicationName,
            backup.CapturedUtc));
    }

    [HttpGet]
    public async Task<IActionResult> ListBackups([FromQuery] string? nodeId, [FromQuery] string? username)
    {
        var query = dbContext.ProgressBackups.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            query = query.Where(x => x.NodeId == nodeId);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(x => x.Username == username);
        }

        var rows = await query
            .OrderByDescending(x => x.CapturedUtc)
            .Take(500)
            .Select(x => new ProgressBackupResponse(x.Id, x.NodeId, x.Username, x.ApplicationName, x.CapturedUtc))
            .ToListAsync();

        return Ok(rows);
    }
}
