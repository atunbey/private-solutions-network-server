namespace Platform.Data.Entities;

public class GroupApplication
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = null!;
}
