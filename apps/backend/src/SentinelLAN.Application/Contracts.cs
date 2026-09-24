using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class Permissions
{
    public const string ManageUsers = "users:manage";
    public const string ManageEnrollment = "enrollment:manage";
    public const string ManageDevices = "devices:manage";
    public const string ViewDevices = "devices:view";
    public const string ViewAssignedDevice = "devices:view-assigned";
    public const string ManageCommands = "commands:manage";
    public const string ViewAudit = "audit:view";
    public const string ViewPolicies = "policies:view";
    public const string ManagePolicies = "policies:manage";
    public const string ViewAlerts = "alerts:view";
    public const string ManageAlerts = "alerts:manage";

    public static bool RoleHas(string role, string permission) => role switch
    {
        "Admin" => true,
        "Technician" => permission is ViewDevices or ManageCommands or ViewPolicies or ManagePolicies or ViewAlerts or ManageAlerts,
        "Employee" => permission is ViewAssignedDevice,
        "Agent" => false,
        _ => false
    };
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Technician = "Technician";
    public const string Employee = "Employee";
    public const string Agent = "Agent";
}

public static class AuthorizationPolicies
{
    public const string Admin = nameof(Admin);
    public const string Technician = nameof(Technician);
    public const string Employee = nameof(Employee);
    public const string Agent = nameof(Agent);
    public const string ViewDevices = nameof(ViewDevices);
    public const string ViewAssignedDevice = nameof(ViewAssignedDevice);
    public const string ManageCommands = nameof(ManageCommands);
    public const string ViewAudit = nameof(ViewAudit);
    public const string ViewPolicies = nameof(ViewPolicies);
    public const string ManagePolicies = nameof(ManagePolicies);
    public const string ViewAlerts = nameof(ViewAlerts);
    public const string ManageAlerts = nameof(ManageAlerts);
}

public readonly record struct ActorContext(Guid UserId, Guid OrganizationId, string Role, string? SecurityStamp = null);
public readonly record struct AgentContext(Guid DeviceId, Guid OrganizationId);

public static class DeviceScope
{
    public static IQueryable<Device> ForActor(IQueryable<Device> devices, ActorContext actor) =>
        devices.Where(device =>
            device.OrganizationId == actor.OrganizationId &&
            (actor.Role != Roles.Employee || (device.AssignedUserId == actor.UserId && !device.IsRevoked)));
}

public record LoginRequest(string OrganizationCode, string Email, string Password);
public record AuthSessionResponse(int ExpiresIn, string Role, string DisplayName);
public record CurrentSessionResponse(string Role, string DisplayName);
public record EnrollRequest(string Token, string DeviceName, string OsVersion, string AgentVersion);
public record EnrollResponse(Guid DeviceId, string DeviceSecret);
public record HeartbeatRequest(string IdempotencyKey, double CpuPercent, double RamPercent, double DiskPercent, string OsVersion, string AgentVersion);
public record CreateCommandRequest(Guid DeviceId, string Type, string Reason, int ValidForSeconds = 120, bool Confirmed = false, string? Parameter = null);
public record CommandResultRequest(bool Succeeded, string Message);
public record CommandDto(Guid Id, Guid DeviceId, string DeviceName, string Type, string Reason, string? Parameter, string Status, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, bool? Succeeded, string? ResultMessage);
public record DeviceDto(Guid Id, string Name, string OsVersion, string AgentVersion, DateTimeOffset? LastSeenAt, bool IsOnline, Guid? AssignedUserId, bool IsRevoked);
public record DashboardDto(int TotalDevices, int OnlineDevices, int OfflineDevices, int OpenAlerts, IReadOnlyList<DeviceDto> Devices);
public record EmployeeDeviceActionDto(string Action, string Reason, string Outcome, DateTimeOffset CreatedAt);
public record EmployeeDeviceDto(DeviceDto Device, string? AppliedPolicy, IReadOnlyList<EmployeeDeviceActionDto> RecentActions);
public record TelemetrySnapshotDto(Guid Id, Guid DeviceId, double CpuPercent, double RamPercent, double DiskPercent, DateTimeOffset CreatedAt);

public record PolicyDto(Guid Id, string Name, int IdleTimeoutMinutes, string UsbMode, int AssignedDeviceCount, DateTimeOffset CreatedAt);
public record CreatePolicyRequest(string Name, int IdleTimeoutMinutes, string UsbMode);
public record UpdatePolicyRequest(string Name, int IdleTimeoutMinutes, string UsbMode);
public record AssignPolicyRequest(Guid PolicyId, Guid DeviceId);

public record AlertDto(Guid Id, Guid? DeviceId, string? DeviceName, string Severity, string Message, bool IsOpen, DateTimeOffset? AcknowledgedAt, DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt);
public record CreateAlertRequest(Guid? DeviceId, string Severity, string Message);

public record AuditLogDto(Guid Id, Guid ActorId, string? ActorName, Guid? DeviceId, string? DeviceName, string Action, string Reason, string Outcome, DateTimeOffset CreatedAt);
public record UserSummaryDto(Guid Id, string Email, string DisplayName, string Role, string Status, DateTimeOffset CreatedAt, bool HasActiveInvitation = false);
public record OrganizationSummaryDto(Guid Id, string Code, string Name, DateTimeOffset CreatedAt);
public record CreateUserRequest
{
    [System.Text.Json.Serialization.JsonConstructor]
    public CreateUserRequest(string email, string displayName, string role, string reason, bool confirmed, string? password = null, int validForHours = 24)
    {
        Email = email;
        DisplayName = displayName;
        Role = role;
        Reason = reason;
        Confirmed = confirmed;
        Password = password;
        ValidForHours = validForHours;
    }

    public CreateUserRequest(string email, string displayName, string role, string password, string reason, bool confirmed)
        : this(email, displayName, role, reason, confirmed, password, 24) { }

    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Confirmed { get; init; }
    public string? Password { get; init; }
    public int ValidForHours { get; init; } = 24;

    public void Deconstruct(out string email, out string displayName, out string role, out string reason, out bool confirmed, out string? password, out int validForHours)
    {
        email = Email;
        displayName = DisplayName;
        role = Role;
        reason = Reason;
        confirmed = Confirmed;
        password = Password;
        validForHours = ValidForHours;
    }
}
public record CreateUserResponse(UserSummaryDto User, string? ActivationToken, string? ActivationUrl, DateTimeOffset? ExpiresAt);
public record SetUserStatusRequest(string Status, string Reason, bool Confirmed);
public record ReissueActivationTokenRequest(string Reason, bool Confirmed, int ValidForHours = 24);
public record ReissueActivationTokenResponse(string ActivationToken, string ActivationUrl, DateTimeOffset ExpiresAt);
public record RevokeActivationTokenRequest(string Reason, bool Confirmed);
public record ActivateAccountRequest(string Token, string Password);
public record ValidateActivationTokenRequest(string? Token);
public record ValidateActivationTokenResponse(bool Valid, string? Message = null);

public record GenerateQrLabelRequest(string Reason, bool Confirmed, int? ValidForDays = null);
public record RevokeQrLabelRequest(string Reason, bool Confirmed);
public record QrLabelResponse(Guid Id, string Code, string CodePrefix, string QrUrl, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
public record QrLabelStatusDto(Guid Id, string CodePrefix, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastScannedAt, bool IsActive);
public record PublicQrResolveDto(string DeviceName, string AssetTag, string AssetStatus, string ContactPolicy, bool IsOnline, bool IsAssigned);
public record AuthenticatedQrResolveDto(Guid DeviceId, string DeviceName, string NextRoute, string Role, bool Authorized, string? Message = null);

public record PrivacyManifestDto(
    IReadOnlyList<string> CollectedTechnicalData,
    IReadOnlyList<string> StrictlyProhibitedData,
    IReadOnlyList<string> AgentPermissions,
    int DataRetentionDays);
public record MyDeviceDto(
    DeviceDto Device,
    string? AppliedPolicy,
    TelemetrySnapshotDto? LatestTelemetry,
    IReadOnlyList<IncidentDto> Incidents,
    IReadOnlyList<EmployeeDeviceActionDto> RecentActions,
    PrivacyManifestDto PrivacyManifest,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? AssetType,
    string? Location,
    DateTimeOffset AssignedAt);
public record ReportMyDeviceIncidentRequest(string Title, string? Description, string Severity = "Medium", string? IdempotencyKey = null);

public record EnrollmentTokenRequest(int ValidForMinutes, string Reason, bool Confirmed);
public record EnrollmentTokenResponse(string Token, DateTimeOffset ExpiresAt);
public record DeviceAssignmentRequest(Guid? AssignedUserId, string Reason, bool Confirmed);
public enum ManagementResultStatus { Succeeded, Invalid, NotFound, Forbidden, Conflict }

public interface ICommandSigner
{
    string Sign(DeviceCommand command);
    bool Verify(DeviceCommand command);
}
