using SentinelLAN.Domain;

namespace SentinelLAN.Application;

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
