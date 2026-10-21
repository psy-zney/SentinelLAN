using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class TechnicalDataRetentionStore(SentinelDbContext db, TimeProvider? clock = null) : ITechnicalDataRetentionStore
{
    public async Task<int> DeleteBatchAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var nonces = await db.Set<AgentRequestNonce>().Where(value => value.ExpiresAt < now.AddMinutes(-1))
            .OrderBy(value => value.ExpiresAt).Take(batchSize).Select(value => value.Id).ToArrayAsync(cancellationToken);
        var adminNonces = await db.Set<AdministrativeRequestNonce>().Where(value => value.ExpiresAt < now.AddDays(-1))
            .OrderBy(value => value.ExpiresAt).Take(batchSize).Select(value => value.Id).ToArrayAsync(cancellationToken);
        var security = await db.SecurityEvents.Where(value => value.CreatedAt < before)
            .OrderBy(value => value.CreatedAt).Take(batchSize).Select(value => value.Id).ToArrayAsync(cancellationToken);
        await DeleteAsync<AgentRequestNonce>(nonces, cancellationToken);
        await DeleteAsync<AdministrativeRequestNonce>(adminNonces, cancellationToken);
        await DeleteAsync<SecurityEvent>(security, cancellationToken);
        var software = await db.Set<DeviceSoftwareObservation>().Where(x => x.CapturedAt < before).OrderBy(x => x.CapturedAt).Take(batchSize).Select(x => x.Id).ToArrayAsync(cancellationToken);
        var outbox = await db.Set<OperationalOutboxMessage>().Where(x => x.ProcessedAt != null && x.ProcessedAt < now.AddDays(-1)).OrderBy(x => x.ProcessedAt).Take(batchSize).Select(x => x.Id).ToArrayAsync(cancellationToken);
        await DeleteAsync<DeviceSoftwareObservation>(software, cancellationToken);
        await DeleteAsync<OperationalOutboxMessage>(outbox, cancellationToken);
        var telemetry = await db.Telemetry.AsNoTracking().Where(t => (t.CollectedAt ?? t.CreatedAt) < before)
            .OrderBy(t => t.CreatedAt).Take(batchSize).Select(t => t.Id).ToListAsync(cancellationToken);
        var heartbeats = await db.Heartbeats.AsNoTracking().Where(h => h.RecordedAt < before)
            .OrderBy(h => h.RecordedAt).Take(batchSize).Select(h => h.Id).ToListAsync(cancellationToken);
        if (db.Database.IsRelational())
        {
            await db.Telemetry.Where(t => telemetry.Contains(t.Id)).ExecuteDeleteAsync(cancellationToken);
            await db.Heartbeats.Where(h => heartbeats.Contains(h.Id)).ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            db.Telemetry.RemoveRange(await db.Telemetry.Where(t => telemetry.Contains(t.Id)).ToListAsync(cancellationToken));
            db.Heartbeats.RemoveRange(await db.Heartbeats.Where(h => heartbeats.Contains(h.Id)).ToListAsync(cancellationToken));
            await db.SaveChangesAsync(cancellationToken);
        }
        return telemetry.Count + heartbeats.Count + nonces.Length + adminNonces.Length + security.Length + software.Length + outbox.Length;
    }

    private async Task DeleteAsync<T>(Guid[] ids, CancellationToken ct) where T : Entity
    {
        if (ids.Length == 0) return;
        var query = db.Set<T>().Where(value => ids.Contains(value.Id));
        if (db.Database.IsRelational()) await query.ExecuteDeleteAsync(ct);
        else db.RemoveRange(await query.ToListAsync(ct));
    }
}
