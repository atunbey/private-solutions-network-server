using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;
using Shared.Contracts.Admin;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin/devices")]
[Authorize]
public class DevicesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDevices()
    {
        var devices = await dbContext.Devices
            .Select(d => new DeviceResponse(d.Id, d.DeviceUuid, d.DisplayName))
            .ToListAsync();
        return Ok(devices);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDevice(Guid id)
    {
        var d = await dbContext.Devices
            .Include(x => x.DeviceAssignments).ThenInclude(da => da.User)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        return Ok(new DeviceDetailResponse(
            d.Id,
            d.DeviceUuid,
            d.DisplayName,
            d.DeviceAssignments.Select(da => new UserSummary(da.User.Id, da.User.Username, da.User.Email)).ToList()));
    }

    [HttpPost]
    [Authorize(Roles = "psn-admin")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceUuid))
            return BadRequest(new { message = "DeviceUuid is required." });

        if (await dbContext.Devices.AnyAsync(d => d.DeviceUuid == request.DeviceUuid.Trim()))
            return Conflict(new { message = "A device with that UUID already exists." });

        var device = new Device
        {
            DeviceUuid = request.DeviceUuid.Trim(),
            DisplayName = request.DisplayName?.Trim() ?? string.Empty
        };
        dbContext.Devices.Add(device);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "device.create", Details = device.DeviceUuid });
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDevice), new { id = device.Id },
            new DeviceResponse(device.Id, device.DeviceUuid, device.DisplayName));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "psn-admin")]
    public async Task<IActionResult> UpdateDevice(Guid id, [FromBody] UpdateDeviceRequest request)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(x => x.Id == id);
        if (device is null)
        {
            return NotFound();
        }

        var deviceUuid = request.DeviceUuid.Trim();
        var displayName = request.DisplayName.Trim();

        if (await dbContext.Devices.AnyAsync(x => x.Id != id && x.DeviceUuid == deviceUuid))
        {
            return Conflict(new { message = "A device with that UUID already exists." });
        }

        device.DeviceUuid = deviceUuid;
        device.DisplayName = displayName;
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "device.update", Details = id.ToString() });
        await dbContext.SaveChangesAsync();

        return Ok(new DeviceResponse(device.Id, device.DeviceUuid, device.DisplayName));
    }

    [HttpPost("{deviceId:guid}/users/{userId:guid}")]
    [Authorize(Roles = "psn-admin")]
    public async Task<IActionResult> AssignUser(Guid deviceId, Guid userId)
    {
        if (!await dbContext.Devices.AnyAsync(d => d.Id == deviceId))
            return NotFound(new { message = "Device not found." });
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId))
            return NotFound(new { message = "User not found." });

        var exists = await dbContext.DeviceAssignments.AnyAsync(da => da.DeviceId == deviceId && da.UserId == userId);
        if (!exists)
        {
            dbContext.DeviceAssignments.Add(new DeviceAssignment { DeviceId = deviceId, UserId = userId });
            dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "device.user.assign", Details = $"device={deviceId},user={userId}" });
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("{deviceId:guid}/users/{userId:guid}")]
    [Authorize(Roles = "psn-admin")]
    public async Task<IActionResult> RemoveUser(Guid deviceId, Guid userId)
    {
        var row = await dbContext.DeviceAssignments.SingleOrDefaultAsync(da => da.DeviceId == deviceId && da.UserId == userId);
        if (row is null) return NoContent();
        dbContext.DeviceAssignments.Remove(row);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "device.user.remove", Details = $"device={deviceId},user={userId}" });
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "psn-admin")]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var device = await dbContext.Devices
            .Include(x => x.DeviceAssignments)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (device is null) return NotFound();

        dbContext.DeviceAssignments.RemoveRange(device.DeviceAssignments);
        dbContext.Devices.Remove(device);
        dbContext.AuditLogs.Add(new AuditLog { Actor = ActorName(), Action = "device.delete", Details = id.ToString() });
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private string ActorName() => User.Identity?.Name ?? "system";
}
