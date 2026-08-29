using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class Permissions
{
    public const string ViewDevices = "devices:view";
    public const string ManageCommands = "commands:manage";
    public const string ViewAudit = "audit:view";
    public static bool RoleHas(string role, string permission) => role switch
    {
        "Admin" => true,
        "Technician" => permission is ViewDevices or ManageCommands,
        "Employee" => false,
        _ => false
    };
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Technician = "Technician";
    public const string Employee = "Employee";
}

public static class AuthorizationPolicies
{
    public const string ViewDevices = nameof(ViewDevices);
    public const string ManageCommands = nameof(ManageCommands);
    public const string ViewAudit = nameof(ViewAudit);
}

public readonly record struct ActorContext(Guid UserId, Guid OrganizationId, string Role);

public static class DeviceScope
{
    public static IQueryable<Device> ForActor(IQueryable<Device> devices, ActorContext actor) =>
        devices.Where(device =>
            device.OrganizationId == actor.OrganizationId &&
            (actor.Role != Roles.Employee || device.AssignedUserId == actor.UserId));
}

public record LoginRequest(string OrganizationCode, string Email, string Password);
public record AuthSessionResponse(int ExpiresIn, string Role, string DisplayName);
public record EnrollRequest(string Token, string DeviceName, string OsVersion, string AgentVersion);
public record EnrollResponse(Guid DeviceId, string DeviceSecret);
public record HeartbeatRequest(Guid DeviceId, string DeviceSecret, string IdempotencyKey, double CpuPercent, double RamPercent, double DiskPercent, string OsVersion, string AgentVersion);
public record CreateCommandRequest(Guid DeviceId, string Type, string Reason, int ValidForSeconds = 120);
public record CommandResultRequest(Guid DeviceId, string DeviceSecret, bool Succeeded, string Message);
public record DeviceDto(Guid Id, string Name, string OsVersion, string AgentVersion, DateTimeOffset? LastSeenAt, bool IsOnline);
public record DashboardDto(int TotalDevices, int OnlineDevices, int OfflineDevices, int OpenAlerts, IReadOnlyList<DeviceDto> Devices);

public interface ICommandSigner
{
    string Sign(DeviceCommand command);
    bool Verify(DeviceCommand command);
}
