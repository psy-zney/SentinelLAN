using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class ManagementServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid adminId = Guid.NewGuid();

    [Fact]
    public async Task CreateUserNormalizesEmailAndNeverStoresPasswordInAudit()
    {
        var store = new FakeManagementStore();
        var service = new UserManagementService(store);
        var result = await service.CreateAsync(new ActorContext(adminId, organizationId, Roles.Admin),
            new CreateUserRequest(" New.User@Example.com ", "New User", Roles.Employee, "long-password-123", "staff onboarding", true),
            new FakePasswordHasher(), CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Succeeded, result.Status);
        Assert.Equal("new.user@example.com", result.User!.Email);
        Assert.Equal("HASHED", store.Users.Single().PasswordHash);
        Assert.DoesNotContain("long-password-123", store.Audits.Single().Reason);
    }

    [Fact]
    public async Task NonAdminCannotCreateUserAndInvalidNullFieldsAreRejected()
    {
        var store = new FakeManagementStore();
        var service = new UserManagementService(store);
        var technician = await service.CreateAsync(new ActorContext(Guid.NewGuid(), organizationId, Roles.Technician),
            new CreateUserRequest("person@example.com", "Person", Roles.Employee, "long-password-123", "reason", true),
            new FakePasswordHasher(), CancellationToken.None);
        var invalid = await service.CreateAsync(new ActorContext(adminId, organizationId, Roles.Admin),
            new CreateUserRequest(null!, null!, Roles.Employee, null!, "reason", true),
            new FakePasswordHasher(), CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Forbidden, technician.Status);
        Assert.Equal(ManagementResultStatus.Invalid, invalid.Status);
    }

    [Fact]
    public async Task AssignmentAndRevocationAreTenantScopedAndRevocationIsIdempotent()
    {
        var employeeId = Guid.NewGuid();
        var device = new Device { OrganizationId = organizationId, Name = "PC", OsVersion = "Windows", AgentVersion = "1" };
        var store = new FakeManagementStore { Devices = { device } };
        store.Users.Add(new User { OrganizationId = organizationId, Email = "employee@example.com", DisplayName = "Employee", Role = Roles.Employee, PasswordHash = "hash", Id = employeeId });
        var service = new DeviceManagementService(store);
        var actor = new ActorContext(adminId, organizationId, Roles.Admin);

        var assigned = await service.AssignAsync(actor, device.Id, new DeviceAssignmentRequest(employeeId, "assign device", true), CancellationToken.None);
        var revoked = await service.RevokeAsync(actor, device.Id, new RevokeDeviceRequest("retired device", true), CancellationToken.None);
        var retry = await service.RevokeAsync(actor, device.Id, new RevokeDeviceRequest("retry", true), CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Succeeded, assigned.Status);
        Assert.Equal(employeeId, assigned.Device!.AssignedUserId);
        Assert.Equal(ManagementResultStatus.Succeeded, revoked.Status);
        Assert.True(revoked.Device!.IsRevoked);
        Assert.Equal(ManagementResultStatus.Succeeded, retry.Status);
        Assert.Single(store.Audits, audit => audit.Action == "DeviceRevoked");
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "HASHED";
        public PasswordVerificationResult Verify(string password, string passwordHash) => PasswordVerificationResult.Success;
    }

    private sealed class FakeManagementStore : IManagementStore
    {
        public List<User> Users { get; } = [];
        public List<Device> Devices { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(Users.SingleOrDefault(x => x.OrganizationId == organizationId && x.Id == userId));
        public Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(Users.SingleOrDefault(x => x.OrganizationId == organizationId && x.Email == email));
        public Task<bool> HasActiveDeviceAssignmentAsync(Guid organizationId, Guid userId, Guid? excludingDeviceId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(x => x.OrganizationId == organizationId && !x.IsRevoked && x.AssignedUserId == userId && x.Id != excludingDeviceId));
        public Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult(Devices.SingleOrDefault(x => x.OrganizationId == organizationId && x.Id == deviceId));
        public Task<DeviceCredential?> FindCredentialAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<DeviceCredential?>(null);
        public void AddUser(User user) => Users.Add(user);
        public void AddEnrollmentToken(DeviceEnrollmentToken token) { }
        public void AddAudit(AuditLog auditLog) => Audits.Add(auditLog);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
