namespace SentinelLAN.Domain;

public abstract class Entity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface ITenantOwned { Guid OrganizationId { get; } }

public sealed class Organization : Entity { public required string Code { get; init; } public required string Name { get; init; } }
public sealed class Department : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required string Name { get; init; } }
public sealed class User : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required string Email { get; init; } public required string DisplayName { get; init; } public required string Role { get; init; } public required string PasswordHash { get; set; } }
public sealed class Role : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required string Name { get; init; } }
public sealed class Permission : Entity { public required string Name { get; init; } }

public sealed class Device : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public required string Name { get; set; }
    public required string OsVersion { get; set; }
    public required string AgentVersion { get; set; }
    public Guid? AssignedUserId { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsRevoked { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsOnline(DateTimeOffset now) => !IsRevoked && LastSeenAt is not null && now - LastSeenAt < TimeSpan.FromMinutes(2);
}

public sealed class DeviceEnrollmentToken : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; private set; }
    public bool TryUse(DateTimeOffset now)
    {
        if (UsedAt is not null || now >= ExpiresAt) return false;
        UsedAt = now;
        return true;
    }
}

public sealed class DeviceCredential : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid DeviceId { get; init; } public required string SecretHash { get; init; } public DateTimeOffset? RevokedAt { get; set; } }
public sealed class DeviceHeartbeat : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid DeviceId { get; init; } public required string IdempotencyKey { get; init; } public DateTimeOffset RecordedAt { get; init; } }
public sealed class TelemetrySnapshot : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid DeviceId { get; init; } public double CpuPercent { get; init; } public double RamPercent { get; init; } public double DiskPercent { get; init; } }
public sealed class Policy : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required string Name { get; init; } public int IdleTimeoutMinutes { get; init; } public required string UsbMode { get; init; } }
public sealed class PolicyAssignment : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid PolicyId { get; init; } public Guid DeviceId { get; init; } }

public enum DeviceCommandStatus { Pending, Delivered, Succeeded, Failed, Expired }
public sealed class DeviceCommand : Entity, ITenantOwned
{
    private static readonly HashSet<string> Allowed = ["ShowNotification", "CollectTelemetryNow", "RefreshPolicy", "SimulateLock", "SimulateNetworkIsolation"];
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public required Guid IssuedByUserId { get; init; }
    public required string Type { get; init; }
    public required string Reason { get; init; }
    public required string Nonce { get; init; }
    public required string Signature { get; set; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DeviceCommandStatus Status { get; set; }
    public bool CanDeliver(DateTimeOffset now) => Allowed.Contains(Type) && Status == DeviceCommandStatus.Pending && now < ExpiresAt;
}

public sealed class CommandResult : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid DeviceId { get; init; } public Guid CommandId { get; init; } public bool Succeeded { get; init; } public required string Message { get; init; } }
public sealed class Alert : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid? DeviceId { get; init; } public required string Severity { get; init; } public required string Message { get; init; } public bool IsOpen { get; set; } = true; }
public sealed class SecurityEvent : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid? DeviceId { get; init; } public required string Type { get; init; } public required string Summary { get; init; } }
public sealed class AuditLog : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required Guid ActorId { get; init; } public Guid? DeviceId { get; init; } public required string Action { get; init; } public required string Reason { get; init; } public required string Outcome { get; set; } }
