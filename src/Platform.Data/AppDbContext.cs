using Microsoft.EntityFrameworkCore;
using Platform.Data.Entities;

namespace Platform.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<GroupApplication> GroupApplications => Set<GroupApplication>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceAssignment> DeviceAssignments => Set<DeviceAssignment>();
    public DbSet<ProgressBackup> ProgressBackups => Set<ProgressBackup>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(x => x.ExternalId).IsUnique();
            b.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Group>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Application>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
            b.HasIndex(x => x.BalenaAppSlug).IsUnique();
        });

        modelBuilder.Entity<UserGroup>(b =>
        {
            b.HasKey(x => new { x.UserId, x.GroupId });
            b.HasOne(x => x.User).WithMany(x => x.UserGroups).HasForeignKey(x => x.UserId);
            b.HasOne(x => x.Group).WithMany(x => x.UserGroups).HasForeignKey(x => x.GroupId);
        });

        modelBuilder.Entity<GroupApplication>(b =>
        {
            b.HasKey(x => new { x.GroupId, x.ApplicationId });
            b.HasOne(x => x.Group).WithMany(x => x.GroupApplications).HasForeignKey(x => x.GroupId);
            b.HasOne(x => x.Application).WithMany(x => x.GroupApplications).HasForeignKey(x => x.ApplicationId);
        });

        modelBuilder.Entity<Device>(b =>
        {
            b.HasIndex(x => x.DeviceUuid).IsUnique();
        });

        modelBuilder.Entity<ProgressBackup>(b =>
        {
            b.HasIndex(x => new { x.NodeId, x.Username, x.ApplicationName, x.CapturedUtc });
            b.Property(x => x.NodeId).HasMaxLength(128);
            b.Property(x => x.Username).HasMaxLength(200);
            b.Property(x => x.ApplicationName).HasMaxLength(200);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(200);
        });

        base.OnModelCreating(modelBuilder);
    }
}
