namespace SentinelLAN.Application;

public interface IOperationalNotificationSink
{
    Task NotifyAdminsAsync(Guid organizationId, string title, string message, string deduplicationKey, CancellationToken ct);
}
