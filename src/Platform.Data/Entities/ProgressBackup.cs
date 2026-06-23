namespace Platform.Data.Entities;

public class ProgressBackup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NodeId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string ProgressJson { get; set; } = "{}";
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
}
