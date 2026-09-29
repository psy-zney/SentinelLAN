using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class PlatformIdentity
{
    public const string OrganizationCode = "_platform";
}

public sealed record PlatformLoginRequest(string Email, string Password);
public sealed record PlatformConfirmation(string Reason, bool Confirmed, Guid Nonce, DateTimeOffset ExpiresAt);
public sealed record CreateCompanyRequest(string Code, string Name, string AdminEmail, string AdminName, PlatformConfirmation Confirmation);
public sealed record SetCompanyStatusRequest(bool IsSuspended, PlatformConfirmation Confirmation);
public sealed record CompanySummary(Guid Id, string Code, string Name, bool IsSuspended, int Users, int Devices, int OnlineDevices, DateTimeOffset? LastSeenAt, DateTimeOffset CreatedAt);
public sealed record CompanyInvitation(Guid OrganizationId, string OrganizationCode, string AdminEmail, string ActivationUrl, DateTimeOffset ExpiresAt);

public sealed record CompanyUserItem(Guid Id, string Email, string DisplayName, string Role, string Status, DateTimeOffset CreatedAt);
public sealed record CompanyDeviceItem(Guid Id, string Name, string OsVersion, string AgentVersion, DateTimeOffset? LastSeenAt, bool IsOnline, bool IsRevoked, string? AssignedUserDisplayName, string? AssetType, string? SerialNumber);
public sealed record CompanyAlertItem(Guid Id, string Severity, string Message, string? DeviceName, bool IsOpen, DateTimeOffset CreatedAt);
public sealed record CompanyAuditItem(Guid Id, string Action, string Reason, string Outcome, DateTimeOffset CreatedAt);
public sealed record CompanyDetailView(Guid Id, string Code, string Name, bool IsSuspended, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, IReadOnlyList<CompanyUserItem> Users, IReadOnlyList<CompanyDeviceItem> Devices, IReadOnlyList<CompanyAlertItem> Alerts, IReadOnlyList<CompanyAuditItem> RecentAudits);

public sealed record VpsNodeHealthItem(Guid Id, string Name, string Host, int Port, string Username, string HostKeyFingerprint, string Status, string? ErrorMessage, DateTimeOffset? LastCheckedAt, double? CpuPercent, double? RamPercent, double? DiskPercent);
public sealed record PlatformSystemStatus(string DatabaseProvider, bool DatabaseConnected, double DatabaseLatencyMs, string HostName, string OsDescription, string RuntimeVersion, double UptimeSeconds, double MemoryUsageMb, int TotalCompanies, int ActiveCompanies, int SuspendedCompanies, int TotalDevices, int OnlineDevices, int TotalUsers, IReadOnlyList<VpsNodeHealthItem> VpsNodes);


public interface IPlatformStore
{
    Task<IReadOnlyList<CompanySummary>> ListAsync(CancellationToken ct);
    Task<CompanyDetailView?> GetCompanyDetailsAsync(Guid id, CancellationToken ct);
    Task<PlatformSystemStatus> GetSystemStatusAsync(CancellationToken ct);
    Task<Organization?> FindAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(string code, string name, CancellationToken ct);
    Task<bool> NonceExistsAsync(Guid actorId, Guid nonce, CancellationToken ct);
    Task RevokeCompanySessionsAsync(Guid id, DateTimeOffset now, CancellationToken ct);
    Task<User?> FindPendingAdminAsync(Guid id, CancellationToken ct);
    Task ReplaceInvitationAsync(AccountActivationToken invitation, DateTimeOffset now, CancellationToken ct);
    void AddCompany(Organization organization, User admin, AccountActivationToken invitation);
    void AddOperation(PlatformOperation operation, AuditLog audit);
    Task<bool> SaveAsync(CancellationToken ct);
}
