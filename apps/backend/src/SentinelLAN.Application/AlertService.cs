using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IAlertStore
{
    Task<IReadOnlyList<AlertDto>> GetAlertsAsync(Guid organizationId, bool? onlyOpen, CancellationToken cancellationToken);
    Task<Alert?> FindAlertAsync(Guid organizationId, Guid alertId, CancellationToken cancellationToken);
    void AddAlert(Alert alert);
    void AddAudit(AuditLog log);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class AlertService(IAlertStore store, TimeProvider timeProvider)
{
    public Task<IReadOnlyList<AlertDto>> GetAlertsAsync(ActorContext actor, bool? onlyOpen, CancellationToken cancellationToken) =>
        store.GetAlertsAsync(actor.OrganizationId, onlyOpen, cancellationToken);

    public async Task<AlertDto?> TriggerAlertAsync(Guid organizationId, Guid? deviceId, string severity, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(severity) || string.IsNullOrWhiteSpace(message)) return null;

        var now = timeProvider.GetUtcNow();
        var alert = new Alert
        {
            OrganizationId = organizationId,
            DeviceId = deviceId,
            Severity = severity.Trim(),
            Message = message.Trim(),
            IsOpen = true
        };

        store.AddAlert(alert);
        store.AddAudit(new AuditLog
        {
            OrganizationId = organizationId,
            ActorId = deviceId ?? Guid.Empty,
            DeviceId = deviceId,
            Action = $"AlertTriggered:{alert.Severity}",
            Reason = alert.Message,
            Outcome = "Open"
        });

        await store.SaveChangesAsync(cancellationToken);
        return new AlertDto(alert.Id, alert.DeviceId, null, alert.Severity, alert.Message, alert.IsOpen, alert.AcknowledgedAt, alert.ResolvedAt, alert.CreatedAt);
    }

    public async Task<bool> AcknowledgeAlertAsync(ActorContext actor, Guid alertId, CancellationToken cancellationToken)
    {
        var alert = await store.FindAlertAsync(actor.OrganizationId, alertId, cancellationToken);
        if (alert is null || !alert.IsOpen) return false;

        var now = timeProvider.GetUtcNow();
        alert.Acknowledge(now);
        alert.UpdatedAt = now;

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = alert.DeviceId,
            Action = "AlertAcknowledged",
            Reason = $"Alert '{alert.Id}' acknowledged by operator",
            Outcome = "Acknowledged"
        });

        await store.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResolveAlertAsync(ActorContext actor, Guid alertId, CancellationToken cancellationToken)
    {
        var alert = await store.FindAlertAsync(actor.OrganizationId, alertId, cancellationToken);
        if (alert is null) return false;

        var now = timeProvider.GetUtcNow();
        alert.Resolve(actor.UserId, now);
        alert.UpdatedAt = now;

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = alert.DeviceId,
            Action = "AlertResolved",
            Reason = $"Alert '{alert.Id}' marked resolved by operator",
            Outcome = "Resolved"
        });

        await store.SaveChangesAsync(cancellationToken);
        return true;
    }
}
