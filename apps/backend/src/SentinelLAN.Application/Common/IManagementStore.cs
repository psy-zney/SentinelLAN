using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IManagementStore
{
    Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
    Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetUsersAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<User>>([]);
    Task<int> CountActiveAdminsAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult(1);
    Task<IReadOnlyList<RefreshSession>> GetActiveUserSessionsAsync(Guid organizationId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RefreshSession>>([]);
    Task<bool> HasActiveDeviceAssignmentAsync(Guid organizationId, Guid userId, Guid? excludingDeviceId, CancellationToken cancellationToken);
    Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<DeviceCredential?> FindCredentialAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddEnrollmentToken(DeviceEnrollmentToken token);
    void AddAudit(AuditLog auditLog);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    // Activation Tokens
    void AddActivationToken(AccountActivationToken token) { }
    Task<AccountActivationToken?> FindActiveActivationTokenByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<AccountActivationToken?>(null);
    Task<AccountActivationToken?> FindActivationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<AccountActivationToken?>(null);
    Task<IReadOnlyList<AccountActivationToken>> GetActiveActivationTokensByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AccountActivationToken>>([]);

    // QR Labels
    void AddQrLabel(DeviceQrLabel label) { }
    Task<DeviceQrLabel?> FindActiveQrLabelByDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<DeviceQrLabel?>(null);
    Task<DeviceQrLabel?> FindQrLabelByHashAsync(string codeHash, CancellationToken cancellationToken) => Task.FromResult<DeviceQrLabel?>(null);
    Task<DeviceQrLabel?> FindTenantQrLabelByHashAsync(Guid organizationId, string codeHash, CancellationToken cancellationToken) => Task.FromResult<DeviceQrLabel?>(null);
}
