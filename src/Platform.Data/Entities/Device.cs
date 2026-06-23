namespace Platform.Data.Entities;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceUuid { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<DeviceAssignment> DeviceAssignments { get; set; } = new List<DeviceAssignment>();
}
