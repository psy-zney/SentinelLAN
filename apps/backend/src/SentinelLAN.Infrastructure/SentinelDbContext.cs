using Microsoft.EntityFrameworkCore;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceEnrollmentToken> EnrollmentTokens => Set<DeviceEnrollmentToken>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<DeviceHeartbeat> Heartbeats => Set<DeviceHeartbeat>();
    public DbSet<TelemetrySnapshot> Telemetry => Set<TelemetrySnapshot>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyAssignment> PolicyAssignments => Set<PolicyAssignment>();
    public DbSet<DeviceCommand> Commands => Set<DeviceCommand>();
    public DbSet<CommandResult> CommandResults => Set<CommandResult>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Organization>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
        modelBuilder.Entity<RefreshSession>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshSession>().HasIndex(x => new { x.FamilyId, x.ExpiresAt });
        modelBuilder.Entity<RefreshSession>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<Device>().HasIndex(x => new { x.OrganizationId, x.AssignedUserId });
        modelBuilder.Entity<DeviceHeartbeat>().HasIndex(x => new { x.DeviceId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<DeviceEnrollmentToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<DeviceCredential>().HasIndex(x => x.DeviceId).IsUnique();
        modelBuilder.Entity<CommandResult>().HasIndex(x => x.CommandId).IsUnique();
        modelBuilder.Entity<DeviceCommand>().HasIndex(x => new { x.DeviceId, x.Nonce }).IsUnique();
        modelBuilder.Entity<Device>().Property(x => x.RowVersion).IsRowVersion();
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(x => typeof(ITenantOwned).IsAssignableFrom(x.ClrType)))
            modelBuilder.Entity(type.ClrType).HasIndex(nameof(ITenantOwned.OrganizationId));
    }
}
