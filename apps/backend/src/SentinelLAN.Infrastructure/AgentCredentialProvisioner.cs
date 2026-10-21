using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AgentCredentialProvisioner(SentinelDbContext db, SensitiveDataCipher cipher)
{
    public async Task<string?> ProvisionAsync(Guid deviceId, string secret, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var credential = await db.DeviceCredentials.SingleOrDefaultAsync(value => value.DeviceId == deviceId && value.RevokedAt == null &&
                db.Devices.Any(device => device.Id == value.DeviceId && device.OrganizationId == value.OrganizationId && !device.IsRevoked &&
                    db.Organizations.Any(org => org.Id == device.OrganizationId && !org.IsSuspended)), ct);
            if (credential is null || !SecretHash.Matches(secret, credential.SecretHash)) return null;
            if (credential.RequestSigningConfirmedAt is not null) return null;
            if (credential.RequestSigningKeyEnvelope is not null)
            {
                var key = AgentRequestKey.Read(cipher, credential.OrganizationId, deviceId, credential.RequestSigningKeyEnvelope);
                try { return Convert.ToBase64String(key); }
                finally { CryptographicOperations.ZeroMemory(key); }
            }
            credential.RequestSigningKeyEnvelope = AgentRequestKey.Create(cipher, credential.OrganizationId, deviceId, out var encoded);
            db.Add(new AuditLog
            {
                OrganizationId = credential.OrganizationId,
                ActorId = deviceId,
                DeviceId = deviceId,
                Action = "AgentRequestSigningProvisioned",
                Reason = "Device proved its existing credential over the authenticated TLS bootstrap route",
                Outcome = "Success"
            });
            try { await db.SaveChangesAsync(ct); return encoded; }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
        }
        return null;
    }
}
