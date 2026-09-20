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

    public Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.OrganizationId == organizationId && user.Email == email, cancellationToken);

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

public sealed class SecretHasher : ISecretHasher
{
    public string Create(string value) => SecretHash.Create(value);
}
