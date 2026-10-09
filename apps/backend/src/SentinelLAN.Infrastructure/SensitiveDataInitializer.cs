using Microsoft.EntityFrameworkCore;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class SensitiveDataInitializer
{
    public static async Task EncryptLegacyDataAsync(SentinelDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.EncryptionKeyId is null) return;
        db.SetLegacyDataReading(true);
        try
        {
            await UpgradeAsync<SelfServiceMessage>(db,
                "SELECT \"Id\" AS \"Value\" FROM \"SelfServiceMessages\" WHERE \"Body\" NOT LIKE 'sln:v1:%' OR \"AuthorName\" NOT LIKE 'sln:v1:%' LIMIT 100",
                [nameof(SelfServiceMessage.Body), nameof(SelfServiceMessage.AuthorName)], ct);
            await UpgradeAsync<SelfServiceRequest>(db,
                "SELECT \"Id\" AS \"Value\" FROM \"SelfServiceRequests\" WHERE \"Title\" NOT LIKE 'sln:v1:%' OR (\"Description\" IS NOT NULL AND \"Description\" NOT LIKE 'sln:v1:%') LIMIT 100",
                [nameof(SelfServiceRequest.Title), nameof(SelfServiceRequest.Description)], ct);
            await UpgradeAsync<SelfServiceAttachment>(db,
                "SELECT \"Id\" AS \"Value\" FROM \"SelfServiceAttachments\" WHERE substring(\"Content\" from 1 for 5) <> decode('534c4e0001', 'hex') OR \"FileName\" NOT LIKE 'sln:v1:%' LIMIT 100",
                [nameof(SelfServiceAttachment.Content), nameof(SelfServiceAttachment.FileName)], ct);
        }
        finally { db.SetLegacyDataReading(false); }
        // Refuse readiness with a changed/lost key even when no plaintext needs upgrading.
        _ = await db.SelfServiceMessages.AsNoTracking().Select(x => x.Body).FirstOrDefaultAsync(ct);
        _ = await db.SelfServiceRequests.AsNoTracking().Select(x => x.Title).FirstOrDefaultAsync(ct);
        _ = await db.SelfServiceAttachments.AsNoTracking().Select(x => x.Content).FirstOrDefaultAsync(ct);
    }

    private static async Task UpgradeAsync<T>(SentinelDbContext db, string query, string[] properties, CancellationToken ct) where T : Entity
    {
        while (true)
        {
            var ids = await db.Database.SqlQueryRaw<Guid>(query).ToListAsync(ct);
            if (ids.Count == 0) return;
            var entities = await db.Set<T>().IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).ToListAsync(ct);
            foreach (var entity in entities)
                foreach (var property in properties) db.Entry(entity).Property(property).IsModified = true;
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }
}
