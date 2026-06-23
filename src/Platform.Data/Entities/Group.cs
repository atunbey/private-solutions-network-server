namespace Platform.Data.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    public ICollection<GroupApplication> GroupApplications { get; set; } = new List<GroupApplication>();
}
