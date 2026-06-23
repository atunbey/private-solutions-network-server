namespace Platform.Data.Entities;

public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string BalenaAppSlug { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public ICollection<GroupApplication> GroupApplications { get; set; } = new List<GroupApplication>();
}
