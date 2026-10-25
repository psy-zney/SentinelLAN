using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class SelfServiceStore(SentinelDbContext db) : ISelfServiceStore
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantLocks = new();
    public async Task<ISelfServiceWriteScope> BeginWriteAsync(Guid organizationId, CancellationToken ct)
    {
        // The PostgreSQL transaction lock protects attempts + consumption + command creation
        // across API replicas, rather than relying on a process-local OTP check.
        if (db.Database.IsRelational())
        {
            var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var lockId = BitConverter.ToInt64(SHA256.HashData(organizationId.ToByteArray()), 0);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
                return new WriteScope(transaction, null);
            }
            catch { await transaction.DisposeAsync(); throw; }
        }
        var gate = TenantLocks.GetOrAdd(organizationId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new WriteScope(null, gate);
    }
    public async Task<IReadOnlyList<SelfServiceRequest>> GetRequestsAsync(Guid org, CancellationToken ct) => await db.Set<SelfServiceRequest>().Where(x => x.OrganizationId == org).ToListAsync(ct);
    public async Task<IReadOnlyList<SelfServiceRequest>> GetRequestPageAsync(Guid org, Guid? user, Guid? device, PageWindow page, bool activeOnly, CancellationToken ct)
    {
        page.Validate();
        return await db.SelfServiceRequests.AsNoTracking().Where(x => x.OrganizationId == org && (user == null || x.UserId == user && x.DeviceId == device) &&
                (!activeOnly || x.Status != "Closed" && x.Status != "Rejected"))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
    }
    public Task<SelfServiceMessage?> FindIdempotentMessageAsync(Guid org, Guid request, Guid user, string key, CancellationToken ct) =>
        db.SelfServiceMessages.SingleOrDefaultAsync(x => x.OrganizationId == org && x.RequestId == request && x.AuthorId == user && x.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<SelfServiceMessage>> GetMessagePageAsync(Guid org, Guid request, PageWindow page, CancellationToken ct)
    {
        page.Validate();
        return await db.SelfServiceMessages.AsNoTracking().Where(x => x.OrganizationId == org && x.RequestId == request)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<SelfServiceNotification>> GetNotificationPageAsync(Guid org, Guid user, PageWindow page, CancellationToken ct)
    {
        page.Validate();
        return await db.SelfServiceNotifications.AsNoTracking().Where(x => x.OrganizationId == org && x.UserId == user)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
    }
    public Task<SelfServiceNotification?> FindNotificationAsync(Guid org, Guid user, Guid id, CancellationToken ct) =>
        db.SelfServiceNotifications.SingleOrDefaultAsync(x => x.OrganizationId == org && x.UserId == user && x.Id == id, ct);
    public Task<SelfServiceNotification?> FindNotificationByKeyAsync(Guid org, Guid user, string key, CancellationToken ct)
    {
        // A recipient can also be an administrator in the same batch. Include
        // notifications queued before SaveChanges so both routes share one record.
        var pending = db.SelfServiceNotifications.Local.SingleOrDefault(x => x.OrganizationId == org && x.UserId == user && x.DeduplicationKey == key);
        return pending is not null ? Task.FromResult<SelfServiceNotification?>(pending)
            : db.SelfServiceNotifications.SingleOrDefaultAsync(x => x.OrganizationId == org && x.UserId == user && x.DeduplicationKey == key, ct);
    }
    public async Task<AttachmentUsage> GetAttachmentUsageAsync(Guid org, Guid user, DateTimeOffset dayStart, CancellationToken ct)
    {
        var usage = await db.SelfServiceAttachments.Where(x => x.OrganizationId == org).GroupBy(x => x.OrganizationId)
            .Select(g => new AttachmentUsage(g.Sum(x => x.UploadedByUserId == user && x.CreatedAt >= dayStart ? (long)Math.Max(x.Size, AttachmentQuotaSettings.MinimumAccountingBytes) : 0),
                g.Sum(x => x.CreatedAt >= dayStart ? (long)Math.Max(x.Size, AttachmentQuotaSettings.MinimumAccountingBytes) : 0),
                g.Sum(x => (long)Math.Max(x.Size, AttachmentQuotaSettings.MinimumAccountingBytes)))).SingleOrDefaultAsync(ct);
        return usage ?? new(0, 0, 0);
    }
    public Task<SelfServiceRequest?> FindRequestAsync(Guid org, Guid id, CancellationToken ct) => db.Set<SelfServiceRequest>().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
    public Task<SelfServiceRequest?> FindIdempotentRequestAsync(Guid org, Guid user, string key, CancellationToken ct) => db.Set<SelfServiceRequest>().SingleOrDefaultAsync(x => x.OrganizationId == org && x.UserId == user && x.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<SelfServiceMessage>> GetMessagesAsync(Guid org, Guid requestId, CancellationToken ct) => await db.Set<SelfServiceMessage>().Where(x => x.OrganizationId == org && x.RequestId == requestId).ToListAsync(ct);
    public async Task<IReadOnlyList<SupportAttachmentDto>> GetAttachmentsAsync(Guid org, Guid requestId, CancellationToken ct) =>
        await db.Set<SelfServiceAttachment>().AsNoTracking()
            .Where(x => x.OrganizationId == org && x.RequestId == requestId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new SupportAttachmentDto(x.Id, x.RequestId, x.FileName, x.ContentType, x.Size, x.CreatedAt))
            .ToListAsync(ct);
    public Task<int> CountAttachmentsAsync(Guid org, Guid requestId, CancellationToken ct) =>
        db.Set<SelfServiceAttachment>().CountAsync(x => x.OrganizationId == org && x.RequestId == requestId, ct);
    public Task<SelfServiceAttachment?> FindAttachmentAsync(Guid org, Guid requestId, Guid id, CancellationToken ct) => db.Set<SelfServiceAttachment>().SingleOrDefaultAsync(x => x.OrganizationId == org && x.RequestId == requestId && x.Id == id, ct);
    public async Task<IReadOnlyList<SelfServiceCatalogApp>> GetCatalogAsync(Guid org, CancellationToken ct) => await db.Set<SelfServiceCatalogApp>().Where(x => x.OrganizationId == org).ToListAsync(ct);
    public Task<SelfServiceCatalogApp?> FindCatalogAppAsync(Guid org, Guid id, CancellationToken ct) => db.Set<SelfServiceCatalogApp>().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
    public async Task<IReadOnlyList<SelfServiceAnnouncement>> GetAnnouncementsAsync(Guid org, CancellationToken ct) => await db.Set<SelfServiceAnnouncement>().Where(x => x.OrganizationId == org).ToListAsync(ct);
    public async Task<IReadOnlyList<SelfServiceAnnouncementReceipt>> GetReceiptsAsync(Guid org, Guid user, CancellationToken ct) => await db.Set<SelfServiceAnnouncementReceipt>().Where(x => x.OrganizationId == org && x.UserId == user).ToListAsync(ct);
    public async Task<IReadOnlyList<SelfServiceNotification>> GetNotificationsAsync(Guid org, Guid user, CancellationToken ct) => await db.Set<SelfServiceNotification>().Where(x => x.OrganizationId == org && x.UserId == user).ToListAsync(ct);
    public Task<SelfServicePushDevice?> FindPushDeviceAsync(string token, CancellationToken ct) => db.Set<SelfServicePushDevice>().SingleOrDefaultAsync(x => x.Token == token, ct);
    public async Task<IReadOnlyList<SelfServicePushDevice>> GetPushDevicesAsync(Guid org, Guid user, CancellationToken ct) => await db.Set<SelfServicePushDevice>().Where(x => x.OrganizationId == org && x.UserId == user).ToListAsync(ct);
    public void Add<T>(T entity) where T : Entity, ITenantOwned => db.Add(entity);
    public void RemovePushDevice(SelfServicePushDevice device) => db.Remove(device);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class WriteScope(IDbContextTransaction? transaction, SemaphoreSlim? gate) : ISelfServiceWriteScope
    {
        public Task CommitAsync(CancellationToken ct) => transaction?.CommitAsync(ct) ?? Task.CompletedTask;
        public async ValueTask DisposeAsync()
        {
            try { if (transaction is not null) await transaction.DisposeAsync(); }
            finally { gate?.Release(); }
        }
    }
}
