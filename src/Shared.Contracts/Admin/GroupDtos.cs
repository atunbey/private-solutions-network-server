namespace Shared.Contracts.Admin;

public record CreateGroupRequest(string Name);

public record GroupResponse(Guid Id, string Name);

public record GroupDetailResponse(
    Guid Id,
    string Name,
    IReadOnlyList<UserSummary> Users,
    IReadOnlyList<ApplicationSummary> Applications);

public record UserSummary(Guid Id, string Username, string Email);
public record ApplicationSummary(Guid Id, string Name, string BalenaAppSlug, bool Enabled);
