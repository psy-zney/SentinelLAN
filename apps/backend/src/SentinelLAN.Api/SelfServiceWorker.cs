using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public sealed class SelfServiceWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, IConfiguration configuration, ILogger<SelfServiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SelfServiceService>().ReconcileAsync(stoppingToken);
                if (configuration.GetValue<bool>("SENTINELLAN_EXPO_PUSH_ENABLED"))
                    await DeliverPushAsync(scope.ServiceProvider.GetRequiredService<SentinelDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Self-service reconciliation or push delivery failed"); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task DeliverPushAsync(SentinelDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await db.SelfServicePushDeliveries.Where(d => d.SentAt == null && d.AbandonedAt == null && d.NextAttemptAt <= now).OrderBy(d => d.NextAttemptAt).Take(20).ToListAsync(ct);
        var client = clients.CreateClient("expo-push");
        foreach (var delivery in due)
        {
            var device = await db.SelfServicePushDevices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == delivery.PushDeviceId && d.OrganizationId == delivery.OrganizationId, ct);
            var notification = await db.SelfServiceNotifications.AsNoTracking().SingleOrDefaultAsync(n => n.Id == delivery.NotificationId && n.OrganizationId == delivery.OrganizationId, ct);
            if (device is null || notification is null || device.UserId != notification.UserId)
            {
                delivery.AbandonedAt = now;
                continue;
            }
            try
            {
                // The lock screen sees only a generic message. Details require an authenticated read.
                using var response = await client.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", new
                {
                    to = device.Token, title = "SentinelLAN", body = "Bạn có thông báo mới từ IT.",
                    data = notification.RequestId is Guid id ? new { requestId = id.ToString("D") } : null
                }, ct);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    Retry(delivery, now);
                    continue;
                }
                using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                if (!response.IsSuccessStatusCode || document is null || !document.RootElement.TryGetProperty("data", out var ticket) ||
                    !ticket.TryGetProperty("status", out var status))
                {
                    delivery.AbandonedAt = now;
                    continue;
                }
                if (status.GetString() == "ok" && ticket.TryGetProperty("id", out var ticketId))
                {
                    delivery.TicketId = ticketId.GetString();
                    delivery.SentAt = now; // Accepted by Expo only; receipt is checked separately.
                }
                else
                {
                    delivery.AbandonedAt = now;
                    if (ticket.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var error) && error.GetString() == "DeviceNotRegistered")
                        db.SelfServicePushDevices.Remove(await db.SelfServicePushDevices.SingleAsync(d => d.Id == device.Id, ct));
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                Retry(delivery, now);
                logger.LogWarning("Expo push transport unavailable; retry scheduled");
            }
        }
        await db.SaveChangesAsync(ct);
        var receipts = await db.SelfServicePushDeliveries.Where(d => d.TicketId != null && d.ReceiptCheckedAt == null && d.SentAt <= now.AddMinutes(-15) && d.AbandonedAt == null).Take(20).ToListAsync(ct);
        if (receipts.Count == 0) return;
        try
        {
            using var response = await client.PostAsJsonAsync("https://exp.host/--/api/v2/push/getReceipts", new { ids = receipts.Select(d => d.TicketId).ToArray() }, ct);
            response.EnsureSuccessStatusCode();
            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            if (document is null || !document.RootElement.TryGetProperty("data", out var data)) return;
            foreach (var receipt in receipts)
            {
                if (!data.TryGetProperty(receipt.TicketId!, out var outcome)) continue;
                receipt.ReceiptCheckedAt = now;
                if (outcome.TryGetProperty("status", out var state) && state.GetString() == "error")
                {
                    receipt.AbandonedAt = now;
                    if (outcome.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var error) && error.GetString() == "DeviceNotRegistered")
                    {
                        var device = await db.SelfServicePushDevices.SingleOrDefaultAsync(d => d.Id == receipt.PushDeviceId, ct);
                        if (device is not null) db.SelfServicePushDevices.Remove(device);
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Expo push receipt check unavailable; will retry");
        }
    }

    private static void Retry(SentinelLAN.Domain.SelfServicePushDelivery delivery, DateTimeOffset now)
    {
        delivery.Attempts++;
        if (delivery.Attempts >= 5) delivery.AbandonedAt = now;
        else delivery.NextAttemptAt = now.AddSeconds(Math.Min(300, 5 * Math.Pow(2, delivery.Attempts)));
    }
}
