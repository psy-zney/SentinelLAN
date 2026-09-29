using Microsoft.EntityFrameworkCore;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options, SentinelLAN.Application.ICurrentTenantProvider? tenantProvider = null) : DbContext(options)
{
    private readonly SentinelLAN.Application.ICurrentTenantProvider? _tenantProvider = tenantProvider;
    private Guid? CurrentOrganizationId => _tenantProvider?.CurrentOrganizationId;
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<PlatformOperation> PlatformOperations => Set<PlatformOperation>();
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
    public DbSet<VpsNode> VpsNodes => Set<VpsNode>();
    public DbSet<VpsActionReservation> VpsActionReservations => Set<VpsActionReservation>();
    public DbSet<IncidentTicket> Incidents => Set<IncidentTicket>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<AssetLoan> AssetLoans => Set<AssetLoan>();
    public DbSet<AccountActivationToken> AccountActivationTokens => Set<AccountActivationToken>();
    public DbSet<DeviceQrLabel> DeviceQrLabels => Set<DeviceQrLabel>();
    public DbSet<SelfServiceRequest> SelfServiceRequests => Set<SelfServiceRequest>();
    public DbSet<SelfServiceMessage> SelfServiceMessages => Set<SelfServiceMessage>();
    public DbSet<SelfServiceAttachment> SelfServiceAttachments => Set<SelfServiceAttachment>();
    public DbSet<SelfServiceCatalogApp> SelfServiceCatalogApps => Set<SelfServiceCatalogApp>();
    public DbSet<SelfServiceAnnouncement> SelfServiceAnnouncements => Set<SelfServiceAnnouncement>();
    public DbSet<SelfServiceAnnouncementReceipt> SelfServiceAnnouncementReceipts => Set<SelfServiceAnnouncementReceipt>();
    public DbSet<SelfServiceNotification> SelfServiceNotifications => Set<SelfServiceNotification>();
    public DbSet<SelfServicePushDevice> SelfServicePushDevices => Set<SelfServicePushDevice>();
    public DbSet<SelfServicePushDelivery> SelfServicePushDeliveries => Set<SelfServicePushDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        SelfServiceModelConfiguration.Configure(modelBuilder);
        modelBuilder.Entity<PlatformOperation>().HasIndex(x => new { x.ActorId, x.Nonce }).IsUnique();
        modelBuilder.Entity<Organization>().Property(x => x.IsSuspended).IsConcurrencyToken();
        modelBuilder.Entity<Organization>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Organization>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
        modelBuilder.Entity<RefreshSession>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshSession>().HasIndex(x => new { x.FamilyId, x.ExpiresAt });
        modelBuilder.Entity<RefreshSession>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<Device>()
            .HasIndex(x => new { x.OrganizationId, x.AssignedUserId })
            .HasFilter("\"AssignedUserId\" IS NOT NULL AND \"IsRevoked\" = FALSE")
            .IsUnique();
        modelBuilder.Entity<DeviceHeartbeat>().HasIndex(x => new { x.DeviceId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<DeviceEnrollmentToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<DeviceEnrollmentToken>().Property(x => x.UsedAt).IsConcurrencyToken();
        modelBuilder.Entity<DeviceCredential>().HasIndex(x => x.DeviceId).IsUnique();
        modelBuilder.Entity<CommandResult>().HasIndex(x => x.CommandId).IsUnique();
        modelBuilder.Entity<DeviceCommand>().HasIndex(x => new { x.DeviceId, x.Nonce }).IsUnique();
        modelBuilder.Entity<DeviceCommand>().Property(x => x.Status).IsConcurrencyToken();
        modelBuilder.Entity<DeviceCommand>().Property(x => x.DeliveryLeaseExpiresAt).IsConcurrencyToken();
        modelBuilder.Entity<DeviceCommand>().HasIndex(x => new { x.DeviceId, x.Status, x.ExpiresAt, x.DeliveryLeaseExpiresAt });
        modelBuilder.Entity<Device>()
            .Property(x => x.RowVersion)
            .IsRequired()
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        modelBuilder.Entity<AccountActivationToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<AccountActivationToken>()
            .HasIndex(x => new { x.OrganizationId, x.UserId })
            .HasFilter("\"UsedAt\" IS NULL AND \"RevokedAt\" IS NULL")
            .IsUnique();
        modelBuilder.Entity<AccountActivationToken>()
            .Property(x => x.RowVersion)
            .IsRequired()
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        modelBuilder.Entity<DeviceQrLabel>().HasIndex(x => x.CodeHash).IsUnique();
        modelBuilder.Entity<DeviceQrLabel>()
            .HasIndex(x => new { x.OrganizationId, x.DeviceId })
            .HasFilter("\"RevokedAt\" IS NULL")
            .IsUnique();
        modelBuilder.Entity<DeviceQrLabel>()
            .Property(x => x.RowVersion)
            .IsRequired()
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        modelBuilder.Entity<VpsNode>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<VpsNode>().HasIndex(x => x.Host);
        modelBuilder.Entity<VpsActionReservation>().HasIndex(x => x.Nonce).IsUnique();
        modelBuilder.Entity<IncidentTicket>().HasIndex(x => new { x.OrganizationId, x.DeviceId });
        modelBuilder.Entity<IncidentTicket>()
            .HasIndex(x => new { x.OrganizationId, x.ReportedByUserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        modelBuilder.Entity<WorkOrder>().HasIndex(x => new { x.OrganizationId, x.DeviceId });
        modelBuilder.Entity<WorkOrder>().HasIndex(x => new { x.OrganizationId, x.WorkOrderNumber }).IsUnique();
        modelBuilder.Entity<AssetLoan>().HasIndex(x => new { x.OrganizationId, x.DeviceId });
        var configureTenantMethod = typeof(SentinelDbContext).GetMethod(nameof(ConfigureTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(x => typeof(ITenantOwned).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(type.ClrType).HasIndex(nameof(ITenantOwned.OrganizationId));
            configureTenantMethod!.MakeGenericMethod(type.ClrType).Invoke(this, new object[] { modelBuilder });
        }
    }

    private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(x =>
            CurrentOrganizationId == null || x.OrganizationId == CurrentOrganizationId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateConcurrencyRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateConcurrencyRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateConcurrencyRowVersions()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit entries are append-only.");
        foreach (var entry in ChangeTracker.Entries<Device>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            entry.Property(device => device.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
        foreach (var entry in ChangeTracker.Entries<AccountActivationToken>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            entry.Property(token => token.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
        foreach (var entry in ChangeTracker.Entries<DeviceQrLabel>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            entry.Property(label => label.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
        foreach (var entry in ChangeTracker.Entries<SelfServiceRequest>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            entry.Property(request => request.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
    }
}
