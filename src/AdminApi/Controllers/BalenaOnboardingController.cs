using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Shared.Contracts.Admin;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin/balena/onboarding")]
[Authorize(Roles = "psn-admin")]
public class BalenaOnboardingController(AppDbContext dbContext) : ControllerBase
{
    [HttpPost("plan")]
    public async Task<IActionResult> BuildPlan([FromBody] BalenaOnboardingRequest request, CancellationToken cancellationToken)
    {
        if (request.DeviceId == Guid.Empty)
        {
            return BadRequest(new { message = "DeviceId is required." });
        }

        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new { message = "UserId is required." });
        }

        var device = await dbContext.Devices
            .SingleOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);
        if (device is null)
        {
            return NotFound(new { message = "Device not found." });
        }

        var user = await dbContext.Users
            .Include(x => x.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.GroupApplications)
                        .ThenInclude(ga => ga.Application)
            .SingleOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var assignedDevice = await dbContext.DeviceAssignments
            .AnyAsync(x => x.DeviceId == device.Id && x.UserId == user.Id, cancellationToken);
        if (!assignedDevice)
        {
            return Conflict(new { message = "The selected user is not assigned to the selected device." });
        }

        var groupSummaries = user.UserGroups
            .Select(ug => ug.Group)
            .OrderBy(g => g.Name)
            .Select(g => new BalenaOnboardingGroupSummary(
                g.Id,
                g.Name,
                g.GroupApplications
                    .Select(ga => ga.Application)
                    .Where(a => a.Enabled)
                    .OrderBy(a => a.Name)
                    .Select(a => new BalenaOnboardingApplicationSummary(a.Id, a.Name, a.BalenaAppSlug, a.ServerAuthoritative))
                    .ToList()))
            .ToList();

        var applications = groupSummaries
            .SelectMany(g => g.Applications)
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .OrderBy(a => a.Name)
            .ToList();

        var serviceName = string.IsNullOrWhiteSpace(request.ServiceName) ? "main" : request.ServiceName.Trim();
        var installerUrl = string.IsNullOrWhiteSpace(request.InstallerUrl)
            ? "https://example.invalid/install-client.sh"
            : request.InstallerUrl.Trim();
        var registrationToken = string.IsNullOrWhiteSpace(request.RegistrationToken)
            ? "replace-with-client-registration-token"
            : request.RegistrationToken.Trim();

        var hostShellCommand = $"balena device ssh {device.DeviceUuid}";
        var serviceShellCommand = $"balena device ssh {device.DeviceUuid} {serviceName}";

        var checklist = new List<BalenaOnboardingChecklistItem>
        {
            new(
                "Register SSH access",
                "Make sure the operator public key is added in openBalena SSH keys or the equivalent self-hosted key store before trying balena device ssh."),
            new(
                "Open device shell",
                $"Use the balena CLI against your self-hosted openBalena instance to reach the host OS with {hostShellCommand} or a running service with {serviceShellCommand}."),
            new(
                "Install client software",
                "Run the generated installer script from inside the device shell so the client can register back to this server."),
            new(
                "Verify runtime",
                "Use the supervisor API to confirm health, host config, and device identity once the client is running."),
            new(
                "Authorize containers",
                "Use the admin portal assignments to keep the device limited to the containers that its user groups are allowed to load.")
        };

        var endpoints = new List<BalenaOnboardingEndpointSummary>
        {
            new("Admin API", "GET", "/api/admin/devices", "List devices available for onboarding."),
            new("Admin API", "GET", "/api/admin/users", "List users that can be tied to a device."),
            new("Admin API", "GET", $"/api/admin/devices/{device.Id}", "Load the selected device and assigned users."),
            new("Admin API", "GET", $"/api/admin/users/{user.Id}", "Load the selected user and group memberships."),
            new("Admin API", "POST", "/api/admin/balena/onboarding/plan", "Build the onboarding packet and install script."),
            new("balena CLI", "SSH", $"balena device ssh {device.DeviceUuid}", "Reach the host OS on the device through your self-hosted openBalena instance."),
            new("Supervisor API", "GET", "/ping", "Confirm the supervisor is alive."),
            new("Supervisor API", "GET", "/v1/device", "Inspect device runtime state and version information."),
            new("Supervisor API", "GET", "/v1/device/host-config", "Read runtime host configuration values."),
            new("Supervisor API", "POST", "/v1/blink", "Identify the device during staged onboarding.")
        };

        var installScript = BuildInstallScript(device.DeviceUuid, serviceName, installerUrl, registrationToken);

        return Ok(new BalenaOnboardingPlanResponse(
            new BalenaOnboardingDeviceSummary(device.Id, device.DeviceUuid, device.DisplayName),
            new BalenaOnboardingUserSummary(user.Id, user.Username, user.Email),
            groupSummaries,
            applications,
            checklist,
            endpoints,
            installScript,
            hostShellCommand,
            serviceShellCommand));
    }

    private static string BuildInstallScript(string deviceUuid, string serviceName, string installerUrl, string registrationToken)
    {
        return $$"""
#!/usr/bin/env bash
set -euo pipefail

DEVICE_UUID="{{deviceUuid}}"
SERVICE_NAME="{{serviceName}}"
INSTALLER_URL="{{installerUrl}}"
REGISTRATION_TOKEN="{{registrationToken}}"

cat <<'INFO'
Balena onboarding runbook

1. Register the operator SSH key in your openBalena SSH key store or equivalent self-hosted access list.
2. Open a shell with: balena device ssh "$DEVICE_UUID"
3. Paste or run this installer from inside the device shell.
4. Confirm the client reports back to the admin API and then load the authorized containers for the assigned groups.
INFO

if [[ -z "$INSTALLER_URL" || "$INSTALLER_URL" == "https://example.invalid/install-client.sh" ]]; then
  echo "Set INSTALLER_URL to the client installer script before running onboarding."
  exit 1
fi

echo "Downloading installer from ${INSTALLER_URL}..."
curl -fsSL "$INSTALLER_URL" | sudo bash -s -- \
  --device-uuid "$DEVICE_UUID" \
  --service-name "$SERVICE_NAME" \
  --registration-token "$REGISTRATION_TOKEN"
""";
    }
}