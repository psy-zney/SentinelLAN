using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class TechnicalDataRetentionStore(SentinelDbContext db) : ITechnicalDataRetentionStore
{
    public async Task<int> DeleteBatchAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken)
    {
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
        return telemetry.Count + heartbeats.Count;
    }
}
