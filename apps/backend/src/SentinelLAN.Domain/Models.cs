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
public static class UserStatuses
{
    public const string PendingActivation = "PendingActivation";
    public const string Active = "Active";
    public const string Locked = "Locked";
}

public sealed class User : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required string PasswordHash { get; set; }
    public string Status { get; set; } = UserStatuses.Active;
    public string? SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class Role : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required string Name { get; init; } }
public sealed class Permission : Entity { public required string Name { get; init; } }

public sealed class AccountActivationToken : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid CreatedByUserId { get; init; }
    public byte[] RowVersion { get; set; } = [];

    public bool IsActive(DateTimeOffset now) => UsedAt is null && RevokedAt is null && now < ExpiresAt;

    public bool TryUse(DateTimeOffset now)
    {
        if (!IsActive(now)) return false;
        UsedAt = now;
        return true;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is null) RevokedAt = now;
    }
}

public sealed class DeviceQrLabel : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public required string CodeHash { get; init; }
    public required string CodePrefix { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? LastScannedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || now < ExpiresAt.Value);

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is null) RevokedAt = now;
    }

    public void RecordScan(DateTimeOffset now)
    {
        LastScannedAt = now;
    }
}

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

    // Extended IT Asset & CMMS Profile
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? AssetType { get; set; } = "Workstation";
    public string? LocationCampus { get; set; }
    public string? LocationBuilding { get; set; }
    public string? LocationFloor { get; set; }
    public string? LocationRoom { get; set; }
    public string AssetStatus { get; set; } = "Active";
    public DateTimeOffset? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public DateTimeOffset? WarrantyExpiresAt { get; set; }
    public string? VendorName { get; set; }
    public string? SpecificationsJson { get; set; }

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
public sealed class Policy : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public required string Name { get; set; }
    public int IdleTimeoutMinutes { get; set; }
    public required string UsbMode { get; set; }
}
public sealed class PolicyAssignment : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid PolicyId { get; init; } public Guid DeviceId { get; init; } }

public enum DeviceCommandStatus { Pending, Delivered, Succeeded, Failed, Expired }
public sealed class DeviceCommand : Entity, ITenantOwned
{
    private static readonly HashSet<string> Allowed = ["ShowNotification", "CollectTelemetryNow", "RefreshPolicy", "SimulateLock", "SimulateNetworkIsolation", "RestartService"];
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public required Guid IssuedByUserId { get; init; }
    public required string Type { get; init; }
    public required string Reason { get; init; }
    public required string Nonce { get; init; }
    public required string Signature { get; set; }
    public string? Parameter { get; set; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DeviceCommandStatus Status { get; set; }
    public bool CanDeliver(DateTimeOffset now) => Allowed.Contains(Type) && Status == DeviceCommandStatus.Pending && now < ExpiresAt;
}

public sealed class CommandResult : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid DeviceId { get; init; } public Guid CommandId { get; init; } public bool Succeeded { get; init; } public required string Message { get; init; } }
public sealed class Alert : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid? DeviceId { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public bool IsOpen { get; set; } = true;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public void Acknowledge(DateTimeOffset now) => AcknowledgedAt = now;
    public void Resolve(Guid userId, DateTimeOffset now) { IsOpen = false; ResolvedAt = now; ResolvedByUserId = userId; }
}
public sealed class SecurityEvent : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public Guid? DeviceId { get; init; } public required string Type { get; init; } public required string Summary { get; init; } }
public sealed class AuditLog : Entity, ITenantOwned { public Guid OrganizationId { get; init; } public required Guid ActorId { get; init; } public Guid? DeviceId { get; init; } public required string Action { get; init; } public required string Reason { get; init; } public required string Outcome { get; set; } }

public sealed class VpsNode : Entity, ITenantOwned
{
    private static readonly HashSet<string> AllowedServices = ["nginx", "docker", "sentinellan-agent", "cron", "systemd-resolved"];

    public Guid OrganizationId { get; init; }
    public required string Name { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; } = 22;
    public required string Username { get; set; }
    public required string EncryptedPrivateKey { get; set; }
    public string? HostKeyFingerprint { get; set; }
    public string Status { get; set; } = "Offline";
    public double? CpuPercent { get; set; }
    public double? RamPercent { get; set; }
    public double? DiskPercent { get; set; }
    public int? DockerContainersCount { get; set; }
    public string? Uptime { get; set; }
    public string? OsInfo { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public static bool IsAllowedService(string serviceName) =>
        !string.IsNullOrWhiteSpace(serviceName) && AllowedServices.Contains(serviceName.Trim().ToLowerInvariant());

    public static IReadOnlyCollection<string> GetAllowedServices() => AllowedServices;
}

public sealed class IncidentTicket : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public required Guid ReportedByUserId { get; init; }
    public Guid? AssignedTechnicianId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }

    public void Resolve(DateTimeOffset now, string? notes = null)
    {
        Status = "Resolved";
        ResolvedAt = now;
        if (!string.IsNullOrWhiteSpace(notes)) ResolutionNotes = notes;
    }
}

public sealed class WorkOrder : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public Guid? IncidentId { get; init; }
    public required string WorkOrderNumber { get; set; }
    public required string Title { get; set; }
    public string Type { get; set; } = "Corrective";
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Scheduled";
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double LaborHours { get; set; }
    public decimal PartsCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal TotalCost => PartsCost + LaborCost;
    public string? ChecklistJson { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedTechnicianId { get; set; }

    public void Complete(DateTimeOffset now, double laborHours, decimal partsCost, decimal laborCost, string? notes = null)
    {
        Status = "Completed";
        CompletedAt = now;
        LaborHours = laborHours;
        PartsCost = partsCost;
        LaborCost = laborCost;
        if (!string.IsNullOrWhiteSpace(notes)) Notes = notes;
    }
}

public sealed class AssetLoan : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public required Guid BorrowerUserId { get; init; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset BorrowedAt { get; set; }
    public DateTimeOffset ExpectedReturnDate { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public string? ConditionBefore { get; set; }
    public string? ConditionAfter { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? Notes { get; set; }

    public void Return(DateTimeOffset now, string? conditionAfter = null)
    {
        Status = "Returned";
        ReturnedAt = now;
        if (!string.IsNullOrWhiteSpace(conditionAfter)) ConditionAfter = conditionAfter;
    }
}
