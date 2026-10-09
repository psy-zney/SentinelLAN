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
        "Employee" => permission is ViewAssignedDevice,
        "Agent" => false,
        _ => false
    };
}
