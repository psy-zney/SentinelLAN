using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class ManagementStore(SentinelDbContext db) : IManagementStore
{
    public Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.OrganizationId == organizationId && user.Id == userId, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.OrganizationId == organizationId && user.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetUsersAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().Where(u => u.OrganizationId == organizationId).OrderBy(u => u.DisplayName).ToListAsync(cancellationToken);

    public Task<int> CountActiveAdminsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Users.CountAsync(user => user.OrganizationId == organizationId && user.Role == Roles.Admin && user.Status == UserStatuses.Active, cancellationToken);

    public async Task<IReadOnlyList<RefreshSession>> GetActiveUserSessionsAsync(Guid organizationId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.RefreshSessions.Where(session => session.OrganizationId == organizationId && session.UserId == userId &&
            session.RevokedAt == null && session.ExpiresAt > now).ToListAsync(cancellationToken);

    public Task<bool> HasActiveDeviceAssignmentAsync(Guid organizationId, Guid userId, Guid? excludingDeviceId, CancellationToken cancellationToken) =>
        db.Devices.AnyAsync(device => device.OrganizationId == organizationId && !device.IsRevoked && device.AssignedUserId == userId &&
            (!excludingDeviceId.HasValue || device.Id != excludingDeviceId.Value), cancellationToken);

    public Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.Devices.SingleOrDefaultAsync(device => device.OrganizationId == organizationId && device.Id == deviceId, cancellationToken);

    public Task<DeviceCredential?> FindCredentialAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.DeviceCredentials.SingleOrDefaultAsync(credential => credential.OrganizationId == organizationId && credential.DeviceId == deviceId, cancellationToken);

    public void AddUser(User user) => db.Users.Add(user);
    public void AddEnrollmentToken(DeviceEnrollmentToken token) => db.EnrollmentTokens.Add(token);
    public void AddAudit(AuditLog auditLog) => db.AuditLogs.Add(auditLog);

    // Activation Tokens
    public void AddActivationToken(AccountActivationToken token) => db.AccountActivationTokens.Add(token);

    public Task<AccountActivationToken?> FindActiveActivationTokenByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.AccountActivationTokens.FirstOrDefaultAsync(
            t => t.OrganizationId == organizationId && t.UserId == userId && t.UsedAt == null && t.RevokedAt == null, cancellationToken);

    public Task<AccountActivationToken?> FindActivationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.AccountActivationTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<AccountActivationToken>> GetActiveActivationTokensByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        await db.AccountActivationTokens
            .Where(t => t.OrganizationId == organizationId && t.UserId == userId && t.UsedAt == null && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

    // QR Labels
    public void AddQrLabel(DeviceQrLabel label) => db.DeviceQrLabels.Add(label);

    public Task<DeviceQrLabel?> FindActiveQrLabelByDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.DeviceQrLabels.FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.DeviceId == deviceId && l.RevokedAt == null, cancellationToken);

    public Task<DeviceQrLabel?> FindQrLabelByHashAsync(string codeHash, CancellationToken cancellationToken) =>
        db.DeviceQrLabels.FirstOrDefaultAsync(l => l.CodeHash == codeHash, cancellationToken);

    public Task<DeviceQrLabel?> FindTenantQrLabelByHashAsync(Guid organizationId, string codeHash, CancellationToken cancellationToken) =>
        db.DeviceQrLabels.FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.CodeHash == codeHash, cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }
}

public sealed class EnrollmentSecretGenerator : IEnrollmentSecretGenerator
{
    public string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

public sealed class ActivationTokenGenerator : IActivationTokenGenerator
{
    public string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string GenerateOpaqueCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public sealed class SecretHasher : ISecretHasher
{
    public string Create(string value) => SecretHash.Create(value);
}
