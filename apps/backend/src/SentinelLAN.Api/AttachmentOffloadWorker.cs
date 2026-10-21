using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public sealed class AttachmentOffloadWorker(IServiceScopeFactory scopes, ILogger<AttachmentOffloadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var objects = scope.ServiceProvider.GetRequiredService<IAttachmentObjectStore>();
                if (!objects.Enabled) continue;
                var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
                var attachments = await db.SelfServiceAttachments.Where(x => x.ObjectKey == null).OrderBy(x => x.CreatedAt).Take(10).ToListAsync(ct);
                foreach (var attachment in attachments)
                {
                    // Persist content first; crashes leave the readable DB copy and retry the same opaque key.
                    await objects.PutAsync(attachment.OrganizationId, attachment.RequestId, attachment.Id, attachment.Content, ct);
                    attachment.ObjectKey = AttachmentObjectStore.Key(attachment.OrganizationId, attachment.RequestId, attachment.Id);
                    attachment.Content = [];
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception error) when (!ct.IsCancellationRequested) { logger.LogWarning(error, "Attachment offload will be retried; the database copy is retained"); }
        }
    }
}
