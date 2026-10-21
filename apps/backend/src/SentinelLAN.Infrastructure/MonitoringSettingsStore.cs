using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class MonitoringSettingsStore(SentinelDbContext db) : IMonitoringSettingsStore
{
    public Task<OrganizationMonitoringSettings?> FindAsync(Guid organizationId, CancellationToken ct) =>
        db.Set<OrganizationMonitoringSettings>().SingleOrDefaultAsync(value => value.OrganizationId == organizationId, ct);
    public Task<bool> NonceExistsAsync(Guid organizationId, Guid nonce, CancellationToken ct) =>
        db.Set<AdministrativeRequestNonce>().AnyAsync(value => value.OrganizationId == organizationId && value.Nonce == nonce, ct);
    public void Add(OrganizationMonitoringSettings settings) => db.Add(settings);
    public void Reserve(AdministrativeRequestNonce nonce) => db.Add(nonce);
    public void Audit(AuditLog audit) => db.Add(audit);
    public async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
