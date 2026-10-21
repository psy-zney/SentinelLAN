using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class QrManagementServiceTests
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly Guid adminId = Guid.NewGuid();
    private readonly Guid techId = Guid.NewGuid();
    private readonly Guid employeeId = Guid.NewGuid();
    private readonly FakeTestStore store = new();

    [Fact]
    public async Task AdminOrTechnicianGeneratesSingleRedactedQrLabel()
    {
        var device = new Device
        {
            OrganizationId = orgId,
            Name = "TEST-PC",
            OsVersion = "Win11",
            AgentVersion = "1.0",
            SerialNumber = "SN-SECRET-12345"
        };
        store.Devices.Add(device);

        var service = new QrManagementService(store);
        var tech = new ActorContext(techId, orgId, Roles.Technician);

        // First label
        var res1 = await service.GenerateOrRotateLabelAsync(tech, device.Id, new GenerateQrLabelRequest("First label", true), CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Succeeded, res1.Status);
        Assert.NotNull(res1.Label);
        Assert.StartsWith("/qr/", res1.Label!.QrUrl);

        // Verify audit does NOT contain raw code
        var audit = store.Audits.Single(a => a.Action == "DeviceQrLabelGenerated");
        Assert.DoesNotContain(res1.Label.Code, audit.Reason);
        Assert.Contains(res1.Label.CodePrefix, audit.Reason);

        // Rotate: Second label revokes first
        var res2 = await service.GenerateOrRotateLabelAsync(tech, device.Id, new GenerateQrLabelRequest("Rotate label", true), CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Succeeded, res2.Status);

        var oldLabel = store.QrLabels.Single(l => l.Id == res1.Label.Id);
        Assert.NotNull(oldLabel.RevokedAt); // Old label revoked

        var newLabel = store.QrLabels.Single(l => l.Id == res2.Label!.Id);
        Assert.Null(newLabel.RevokedAt); // New label active
    }

    [Fact]
    public async Task EmployeeCannotGenerateQrLabel()
    {
        var device = new Device { OrganizationId = orgId, Name = "PC", OsVersion = "Win", AgentVersion = "1" };
        store.Devices.Add(device);
        var service = new QrManagementService(store);
        var emp = new ActorContext(employeeId, orgId, Roles.Employee);

        var result = await service.GenerateOrRotateLabelAsync(emp, device.Id, new GenerateQrLabelRequest("reason", true), CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task PublicResolveOmitsPrivateDeviceData()
    {
        var device = new Device
        {
            OrganizationId = orgId,
            Name = "FINANCE-LAPTOP",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            SerialNumber = "CONFIDENTIAL-SERIAL-9999",
            AssignedUserId = employeeId
        };
        store.Devices.Add(device);

        var service = new QrManagementService(store);
        var admin = new ActorContext(adminId, orgId, Roles.Admin);
        var generated = await service.GenerateOrRotateLabelAsync(admin, device.Id, new GenerateQrLabelRequest("Public test", true), CancellationToken.None);

        var publicInfo = await service.ResolvePublicAsync(generated.Label!.Code, CancellationToken.None);
        Assert.NotNull(publicInfo);
        Assert.Equal("FINANCE-LAPTOP", publicInfo!.DeviceName);
        Assert.DoesNotContain("CONFIDENTIAL-SERIAL-9999", publicInfo.AssetTag); // Full serial redacted!
        Assert.True(publicInfo.IsAssigned);
    }

    [Fact]
    public async Task AuthenticatedResolveRoutesByRoleAndAssignment()
    {
        var device = new Device
        {
            OrganizationId = orgId,
            Name = "EMP-DEVICE",
            OsVersion = "Win11",
            AgentVersion = "0.1.0",
            AssignedUserId = employeeId
        };
        store.Devices.Add(device);

        var service = new QrManagementService(store);
        var admin = new ActorContext(adminId, orgId, Roles.Admin);
        var generated = await service.GenerateOrRotateLabelAsync(admin, device.Id, new GenerateQrLabelRequest("Route test", true), CancellationToken.None);
        var code = generated.Label!.Code;

        // 1. Admin resolve -> /devices/{id}
        var adminResolve = await service.ResolveAuthenticatedAsync(admin, code, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Succeeded, adminResolve.Status);
        Assert.Equal($"/devices/{device.Id}", adminResolve.Result!.NextRoute);

        // 2. Assigned employee resolve -> /my-device (Authorized: true)
        var assignedEmp = new ActorContext(employeeId, orgId, Roles.Employee);
        var empResolve = await service.ResolveAuthenticatedAsync(assignedEmp, code, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Succeeded, empResolve.Status);
        Assert.True(empResolve.Result!.Authorized);
        Assert.Equal("/my-device", empResolve.Result.NextRoute);

        // 3. Other employee resolve -> generic Forbidden without leaking device details
        var otherEmp = new ActorContext(Guid.NewGuid(), orgId, Roles.Employee);
        var otherResolve = await service.ResolveAuthenticatedAsync(otherEmp, code, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Forbidden, otherResolve.Status);
        Assert.Null(otherResolve.Result);

        // 4. Other tenant resolve -> NotFound
        var otherTenantAdmin = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), Roles.Admin);
        var otherTenantResolve = await service.ResolveAuthenticatedAsync(otherTenantAdmin, code, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.NotFound, otherTenantResolve.Status);
    }

    private sealed class FakeTestStore : IManagementStore
    {
        public List<User> Users { get; } = [];
        public List<AccountActivationToken> ActivationTokens { get; } = [];
        public List<Device> Devices { get; } = [];
        public List<DeviceQrLabel> QrLabels { get; } = [];
        public List<AuditLog> Audits { get; } = [];

        public Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.OrganizationId == organizationId && u.Id == userId));

        public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

        public Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.OrganizationId == organizationId && u.Email == email));

        public Task<IReadOnlyList<User>> GetUsersAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<User>>(Users.Where(u => u.OrganizationId == organizationId).ToList());

        public Task<bool> HasActiveDeviceAssignmentAsync(Guid organizationId, Guid userId, Guid? excludingDeviceId, CancellationToken cancellationToken) =>
            Task.FromResult(Devices.Any(d => d.OrganizationId == organizationId && !d.IsRevoked && d.AssignedUserId == userId && d.Id != excludingDeviceId));

        public Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.OrganizationId == organizationId && d.Id == deviceId));

        public Task<DeviceCredential?> FindCredentialAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult<DeviceCredential?>(null);

        public void AddUser(User user) => Users.Add(user);
        public void AddEnrollmentToken(DeviceEnrollmentToken token) { }
        public void AddAudit(AuditLog auditLog) => Audits.Add(auditLog);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public void AddActivationToken(AccountActivationToken token) => ActivationTokens.Add(token);

        public Task<AccountActivationToken?> FindActiveActivationTokenByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(ActivationTokens.FirstOrDefault(t => t.OrganizationId == organizationId && t.UserId == userId && t.UsedAt == null && t.RevokedAt == null));

        public Task<AccountActivationToken?> FindActivationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(ActivationTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task<IReadOnlyList<AccountActivationToken>> GetActiveActivationTokensByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AccountActivationToken>>(ActivationTokens.Where(t => t.OrganizationId == organizationId && t.UserId == userId && t.UsedAt == null && t.RevokedAt == null).ToList());

        public void AddQrLabel(DeviceQrLabel label) => QrLabels.Add(label);

        public Task<DeviceQrLabel?> FindActiveQrLabelByDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(QrLabels.FirstOrDefault(l => l.OrganizationId == organizationId && l.DeviceId == deviceId && l.RevokedAt == null));

        public Task<DeviceQrLabel?> FindQrLabelByHashAsync(string codeHash, CancellationToken cancellationToken) =>
            Task.FromResult(QrLabels.FirstOrDefault(l => l.CodeHash == codeHash));

        public Task<DeviceQrLabel?> FindTenantQrLabelByHashAsync(Guid organizationId, string codeHash, CancellationToken cancellationToken) =>
            Task.FromResult(QrLabels.FirstOrDefault(l => l.OrganizationId == organizationId && l.CodeHash == codeHash));
    }
}
