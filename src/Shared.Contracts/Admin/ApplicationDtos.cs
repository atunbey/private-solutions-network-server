namespace Shared.Contracts.Admin;

public record CreateApplicationRequest(string Name, string BalenaAppSlug, bool ServerAuthoritative = false);

public record UpdateApplicationRequest(string Name, string BalenaAppSlug, bool Enabled, bool ServerAuthoritative);

public record ApplicationResponse(Guid Id, string Name, string BalenaAppSlug, bool Enabled, bool ServerAuthoritative);
