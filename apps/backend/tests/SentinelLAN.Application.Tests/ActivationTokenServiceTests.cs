using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class ActivationTokenServiceTests
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly Guid adminId = Guid.NewGuid();
    private readonly FakeTestStore store = new();
    private readonly FakePasswordHasher hasher = new();

    [Fact]
    public async Task AdminCreatesPendingUserWithSingleUseToken()
    {
        var service = new UserManagementService(store);
        var actor = new ActorContext(adminId, orgId, Roles.Admin);
        var request = new CreateUserRequest("invitee@example.com", "Invitee", Roles.Employee, "onboarding new hire", true);

        var result = await service.CreateAsync(actor, request, hasher, CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Succeeded, result.Status);
        Assert.NotNull(result.User);
        Assert.Equal(UserStatuses.PendingActivation, result.User!.Status);
        Assert.NotNull(result.ActivationToken);
        Assert.StartsWith("/activate?token=", result.ActivationUrl);

        var userInStore = store.Users.Single(u => u.Id == result.User.Id);
        Assert.Equal(UserStatuses.PendingActivation, userInStore.Status);

        var tokenInStore = store.ActivationTokens.Single(t => t.UserId == userInStore.Id);
        Assert.NotEqual(result.ActivationToken, tokenInStore.TokenHash); // Hash-only in DB!
        Assert.Null(tokenInStore.UsedAt);
        Assert.Null(tokenInStore.RevokedAt);
    }

    [Fact]
    public async Task NonAdminCannotCreateUserOrReissueToken()
    {
        var service = new UserManagementService(store);
        var tech = new ActorContext(Guid.NewGuid(), orgId, Roles.Technician);
        var emp = new ActorContext(Guid.NewGuid(), orgId, Roles.Employee);

        var createResult = await service.CreateAsync(tech, new CreateUserRequest("a@b.com", "A", Roles.Employee, "reason", true), hasher, CancellationToken.None);
        var reissueResult = await service.ReissueActivationTokenAsync(emp, Guid.NewGuid(), new ReissueActivationTokenRequest("reason", true), CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Forbidden, createResult.Status);
        Assert.Equal(ManagementResultStatus.Forbidden, reissueResult.Status);
    }

    [Fact]
    public async Task ValidateTokenReflectsActiveExpiredAndRevokedState()
    {
        var service = new UserManagementService(store);
        var actor = new ActorContext(adminId, orgId, Roles.Admin);
        var created = await service.CreateAsync(actor, new CreateUserRequest("valid@example.com", "Valid", Roles.Employee, "test reason", true), hasher, CancellationToken.None);

        var validRes = await service.ValidateActivationTokenAsync(created.ActivationToken!, CancellationToken.None);
        Assert.True(validRes.Valid);

        var invalidRes = await service.ValidateActivationTokenAsync("non-existent-token", CancellationToken.None);
        Assert.False(invalidRes.Valid);

        // Manually expire token
        store.ActivationTokens.Single().Revoke(DateTimeOffset.UtcNow);
        var revokedRes = await service.ValidateActivationTokenAsync(created.ActivationToken!, CancellationToken.None);
        Assert.False(revokedRes.Valid);
    }

    [Fact]
    public async Task ActivationSucceedsOnceWithValidPassword()
    {
        var service = new UserManagementService(store);
        var actor = new ActorContext(adminId, orgId, Roles.Admin);
        var created = await service.CreateAsync(actor, new CreateUserRequest("user@example.com", "User", Roles.Employee, "hire", true), hasher, CancellationToken.None);

        // Short password rejected
        var shortPass = await service.ActivateAccountAsync(new ActivateAccountRequest(created.ActivationToken!, "short"), hasher, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Invalid, shortPass.Status);

        // Valid activation
        var success = await service.ActivateAccountAsync(new ActivateAccountRequest(created.ActivationToken!, "correct-horse-battery-staple-12"), hasher, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Succeeded, success.Status);

        var user = store.Users.Single(u => u.Id == created.User!.Id);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal("HASHED:correct-horse-battery-staple-12", user.PasswordHash);

        // Replay / Reuse must fail!
        var replay = await service.ActivateAccountAsync(new ActivateAccountRequest(created.ActivationToken!, "correct-horse-battery-staple-12"), hasher, CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Invalid, replay.Status);
    }

    [Fact]
    public async Task ReissueRevokesPriorTokensAndCreatesReplacement()
    {
        var service = new UserManagementService(store);
        var actor = new ActorContext(adminId, orgId, Roles.Admin);
        var created = await service.CreateAsync(actor, new CreateUserRequest("reissue@example.com", "Reissue", Roles.Employee, "hire", true), hasher, CancellationToken.None);

        var reissued = await service.ReissueActivationTokenAsync(actor, created.User!.Id, new ReissueActivationTokenRequest("lost token", true), CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Succeeded, reissued.Status);
        Assert.NotNull(reissued.Response);

        // Old token cannot validate
        var oldValidate = await service.ValidateActivationTokenAsync(created.ActivationToken!, CancellationToken.None);
        Assert.False(oldValidate.Valid);

        // New token validates
        var newValidate = await service.ValidateActivationTokenAsync(reissued.Response!.ActivationToken, CancellationToken.None);
        Assert.True(newValidate.Valid);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"HASHED:{password}";
        public PasswordVerificationResult Verify(string password, string passwordHash) =>
            passwordHash == $"HASHED:{password}" ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
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
