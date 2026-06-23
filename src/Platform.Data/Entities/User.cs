namespace Platform.Data.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    public ICollection<DeviceAssignment> DeviceAssignments { get; set; } = new List<DeviceAssignment>();
}
