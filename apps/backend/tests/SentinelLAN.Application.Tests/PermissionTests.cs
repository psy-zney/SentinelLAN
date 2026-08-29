using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class PermissionTests
{
    [Theory]
    [InlineData("Admin", Permissions.ViewAudit, true)]
    [InlineData("Technician", Permissions.ManageCommands, true)]
    [InlineData("Technician", Permissions.ViewAudit, false)]
    [InlineData("Employee", Permissions.ViewDevices, false)]
    public void RolePermissionsAreExplicit(string role, string permission, bool expected) => Assert.Equal(expected, Permissions.RoleHas(role, permission));

    [Fact]
    public void DeviceScopeCombinesTenantAndEmployeeAssignment()
    {
        var organizationId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var visible = new Device { OrganizationId = organizationId, AssignedUserId = employeeId, Name = "Visible", OsVersion = "Windows", AgentVersion = "1" };
        var otherEmployee = new Device { OrganizationId = organizationId, AssignedUserId = Guid.NewGuid(), Name = "Other employee", OsVersion = "Windows", AgentVersion = "1" };
        var otherTenant = new Device { OrganizationId = Guid.NewGuid(), AssignedUserId = employeeId, Name = "Other tenant", OsVersion = "Windows", AgentVersion = "1" };

        var result = DeviceScope.ForActor(new[] { visible, otherEmployee, otherTenant }.AsQueryable(), new ActorContext(employeeId, organizationId, Roles.Employee)).ToList();

        Assert.Single(result);
        Assert.Equal(visible.Id, result[0].Id);
    }
}
