using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Data;
using Platform.Data.Entities;
using Shared.Contracts.Admin;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin/balena/onboarding")]
[Authorize(Roles = "psn-admin")]
public class BalenaOnboardingController(AppDbContext dbContext, IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpPost("discover")]
    public async Task<IActionResult> DiscoverDevices([FromBody] BalenaDeviceDiscoveryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiBaseUrl))
        {
            return BadRequest(new { message = "ApiBaseUrl is required." });
        }

        if (string.IsNullOrWhiteSpace(request.AdminToken))
        {
            return BadRequest(new { message = "AdminToken is required." });
        }

        var registeredDevices = await dbContext.Devices
            .AsNoTracking()
            .OrderBy(d => d.DisplayName)
            .ThenBy(d => d.DeviceUuid)
            .Select(d => new BalenaRegisteredDeviceSummary(d.Id, d.DeviceUuid, d.DisplayName))
            .ToListAsync(cancellationToken);

        var pulledDevices = await FetchBalenaDevicesAsync(request.ApiBaseUrl.Trim(), request.AdminToken.Trim(), cancellationToken);

        var registeredUuids = new HashSet<string>(
            registeredDevices.Select(d => d.DeviceUuid.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var discoveredDevices = pulledDevices
            .Where(d => !registeredUuids.Contains(d.DeviceUuid.Trim()))
            .OrderBy(d => d.DisplayName)
            .ThenBy(d => d.DeviceUuid)
            .ToList();

        return Ok(new BalenaDeviceDiscoveryResponse(registeredDevices, discoveredDevices));
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterDiscoveredDevice([FromBody] RegisterDiscoveredDeviceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceUuid))
        {
            return BadRequest(new { message = "DeviceUuid is required." });
        }

        var deviceUuid = request.DeviceUuid.Trim();
        var displayName = request.DisplayName?.Trim() ?? string.Empty;

        if (await dbContext.Devices.AnyAsync(d => d.DeviceUuid == deviceUuid, cancellationToken))
        {
            return Conflict(new { message = "A device with that UUID already exists." });
        }

        var device = new Device
        {
            DeviceUuid = deviceUuid,
            DisplayName = displayName
        };

        dbContext.Devices.Add(device);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Actor = ActorName(),
            Action = "device.register.from_openbalena",
            Details = deviceUuid
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(DevicesController.GetDevice),
            "Devices",
            new { id = device.Id },
            new DeviceResponse(device.Id, device.DeviceUuid, device.DisplayName));
    }

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

    private async Task<List<BalenaDiscoveredDeviceSummary>> FetchBalenaDevicesAsync(string apiBaseUrl, string adminToken, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var paths = new[]
        {
            "/v6/device?$select=id,uuid,device_name,is_online,status",
            "/v6/device"
        };

        var lastError = string.Empty;

        foreach (var path in paths)
        {
            var endpoint = BuildApiEndpoint(apiBaseUrl, path);
            using var response = await client.GetAsync(endpoint, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                lastError = $"{(int)response.StatusCode} {response.ReasonPhrase} | {body}";
                continue;
            }

            if (TryExtractBalenaDevices(body, out var devices) && devices.Count > 0)
            {
                return devices;
            }

            lastError = "The API response did not contain any parseable devices.";
        }

        throw new InvalidOperationException($"Could not pull devices from openBalena. {lastError}");
    }

    private static string BuildApiEndpoint(string apiBaseUrl, string path)
    {
        var baseUrl = apiBaseUrl.Trim().TrimEnd('/');
        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = $"https://{baseUrl}";
        }

        return $"{baseUrl}{path}";
    }

    private static bool TryExtractBalenaDevices(string payload, out List<BalenaDiscoveredDeviceSummary> devices)
    {
        devices = [];

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        IEnumerable<JsonElement> sourceItems = [];

        if (root.ValueKind == JsonValueKind.Array)
        {
            sourceItems = root.EnumerateArray();
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("d", out var dNode) && dNode.ValueKind == JsonValueKind.Array)
            {
                sourceItems = dNode.EnumerateArray();
            }
            else if (root.TryGetProperty("d", out var dWrapper)
                     && dWrapper.ValueKind == JsonValueKind.Object
                     && dWrapper.TryGetProperty("results", out var results)
                     && results.ValueKind == JsonValueKind.Array)
            {
                sourceItems = results.EnumerateArray();
            }
            else if (root.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Array)
            {
                sourceItems = dataNode.EnumerateArray();
            }
        }

        var seenUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in sourceItems)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var uuid = GetString(item, "uuid") ?? GetString(item, "device_uuid");
            if (string.IsNullOrWhiteSpace(uuid) || !seenUuids.Add(uuid.Trim()))
            {
                continue;
            }

            var displayName = GetString(item, "device_name")
                              ?? GetString(item, "deviceName")
                              ?? uuid;

            var balenaId = GetString(item, "id");
            var status = GetString(item, "status");
            var isOnline = GetBool(item, "is_online") ?? GetBool(item, "isOnline");

            devices.Add(new BalenaDiscoveredDeviceSummary(
                uuid.Trim(),
                displayName?.Trim() ?? uuid.Trim(),
                balenaId,
                isOnline,
                status));
        }

        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when property.TryGetInt32(out var intValue) => intValue != 0,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var boolValue) => boolValue,
            JsonValueKind.String when int.TryParse(property.GetString(), out var stringIntValue) => stringIntValue != 0,
            _ => null
        };
    }

    private string ActorName() => User.Identity?.Name ?? "system";
}