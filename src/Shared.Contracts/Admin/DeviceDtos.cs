namespace Shared.Contracts.Admin;

public record CreateDeviceRequest(string DeviceUuid, string DisplayName);
public record UpdateDeviceRequest(string DeviceUuid, string DisplayName);

public record DeviceResponse(Guid Id, string DeviceUuid, string DisplayName);

public record DeviceDetailResponse(
    Guid Id,
    string DeviceUuid,
    string DisplayName,
    IReadOnlyList<UserSummary> AssignedUsers);
