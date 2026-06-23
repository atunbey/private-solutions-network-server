namespace Platform.Data.Entities;

public class DeviceAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;
}
