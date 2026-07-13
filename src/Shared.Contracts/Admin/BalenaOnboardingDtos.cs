namespace Shared.Contracts.Admin;

public record BalenaOnboardingRequest(
    Guid DeviceId,
    Guid UserId,
    string InstallerUrl,
    string RegistrationToken,
    string ServiceName);

public record BalenaOnboardingPlanResponse(
    BalenaOnboardingDeviceSummary Device,
    BalenaOnboardingUserSummary User,
    IReadOnlyList<BalenaOnboardingGroupSummary> Groups,
    IReadOnlyList<BalenaOnboardingApplicationSummary> Applications,
    IReadOnlyList<BalenaOnboardingChecklistItem> Checklist,
    IReadOnlyList<BalenaOnboardingEndpointSummary> Endpoints,
    string InstallScript,
    string HostShellCommand,
    string ServiceShellCommand);

public record BalenaOnboardingDeviceSummary(Guid Id, string DeviceUuid, string DisplayName);

public record BalenaOnboardingUserSummary(Guid Id, string Username, string Email);

public record BalenaOnboardingGroupSummary(
    Guid Id,
    string Name,
    IReadOnlyList<BalenaOnboardingApplicationSummary> Applications);

public record BalenaOnboardingApplicationSummary(
    Guid Id,
    string Name,
    string BalenaAppSlug,
    bool ServerAuthoritative);

public record BalenaOnboardingChecklistItem(string Title, string Detail);

public record BalenaOnboardingEndpointSummary(string System, string Method, string Path, string Purpose);

public record BalenaDeviceDiscoveryRequest(string ApiBaseUrl, string AdminToken);

public record BalenaRegisteredDeviceSummary(Guid Id, string DeviceUuid, string DisplayName);

public record BalenaDiscoveredDeviceSummary(
    string DeviceUuid,
    string DisplayName,
    string? BalenaDeviceId,
    bool? IsOnline,
    string? Status);

public record BalenaDeviceDiscoveryResponse(
    IReadOnlyList<BalenaRegisteredDeviceSummary> RegisteredDevices,
    IReadOnlyList<BalenaDiscoveredDeviceSummary> DiscoveredDevices);

public record RegisterDiscoveredDeviceRequest(string DeviceUuid, string DisplayName);