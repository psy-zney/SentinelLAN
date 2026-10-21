using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AdministrativeRequestJournal(SentinelDbContext db) : IAdministrativeRequestJournal
{
    public Task<bool> NonceExistsAsync(Guid org, Guid nonce, CancellationToken ct) =>
        db.Set<AdministrativeRequestNonce>().AnyAsync(value => value.OrganizationId == org && value.Nonce == nonce, ct);
    public void Reserve(AdministrativeRequestNonce nonce) => db.Add(nonce);
    public void Audit(AuditLog audit) => db.Add(audit);
    public async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
