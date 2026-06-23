namespace Shared.Contracts.Admin;

public record CreateUserRequest(string ExternalId, string Username, string Email);
public record UpdateUserRequest(string ExternalId, string Username, string Email);

public record UserResponse(Guid Id, string ExternalId, string Username, string Email, DateTime CreatedUtc);

public record UserDetailResponse(
    Guid Id,
    string ExternalId,
    string Username,
    string Email,
    DateTime CreatedUtc,
    IReadOnlyList<GroupSummary> Groups,
    IReadOnlyList<DeviceSummary> Devices);

public record GroupSummary(Guid Id, string Name);
public record DeviceSummary(Guid Id, string DeviceUuid, string DisplayName);
