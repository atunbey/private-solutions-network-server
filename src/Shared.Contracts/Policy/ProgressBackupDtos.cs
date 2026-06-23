namespace Shared.Contracts.Policy;

public record ProgressBackupRequest(string NodeId, string Username, string ApplicationName, string ProgressJson, DateTime? CapturedUtc);

public record ProgressBackupResponse(Guid Id, string NodeId, string Username, string ApplicationName, DateTime CapturedUtc);
