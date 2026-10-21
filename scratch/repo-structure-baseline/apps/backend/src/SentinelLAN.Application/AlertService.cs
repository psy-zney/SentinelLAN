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

public interface IAlertDeviceLookup
{
    Task<bool> DeviceExistsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> GetDeviceNamesAsync(Guid organizationId, IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken);
}

public sealed class AlertService(IAlertStore store, IAlertDeviceLookup deviceLookup, TimeProvider timeProvider)
{
    public Task<IReadOnlyList<AlertDto>> GetAlertsAsync(ActorContext actor, bool? onlyOpen, CancellationToken cancellationToken) =>
        Permissions.RoleHas(actor.Role, Permissions.ViewAlerts)
            ? store.GetAlertsAsync(actor.OrganizationId, onlyOpen, cancellationToken)
            : Task.FromResult<IReadOnlyList<AlertDto>>([]);

    public async Task<AlertDto?> TriggerAlertAsync(ActorContext actor, Guid? deviceId, string severity, string message, CancellationToken cancellationToken)
    {
        if (!Permissions.RoleHas(actor.Role, Permissions.ManageAlerts)) return null;
        if (string.IsNullOrWhiteSpace(severity) || string.IsNullOrWhiteSpace(message)) return null;

        var normalizedSeverity = severity.Trim();
        var normalizedMessage = message.Trim();
        if (!AllowedSeverities.Contains(normalizedSeverity, StringComparer.Ordinal) || normalizedMessage.Length is < 1 or > 1000) return null;
        if (deviceId is Guid targetDeviceId && !await deviceLookup.DeviceExistsAsync(actor.OrganizationId, targetDeviceId, cancellationToken)) return null;

        var now = timeProvider.GetUtcNow();
        var alert = new Alert
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = deviceId,
            Severity = normalizedSeverity,
            Message = normalizedMessage,
            IsOpen = true
        };

        store.AddAlert(alert);
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
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
        if (!Permissions.RoleHas(actor.Role, Permissions.ManageAlerts)) return false;
        var alert = await store.FindAlertAsync(actor.OrganizationId, alertId, cancellationToken);
        if (alert is null) return false;
        if (alert.AcknowledgedAt.HasValue) return true;
        if (!alert.IsOpen) return false;

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
        if (!Permissions.RoleHas(actor.Role, Permissions.ManageAlerts)) return false;
        var alert = await store.FindAlertAsync(actor.OrganizationId, alertId, cancellationToken);
        if (alert is null) return false;
        if (alert.ResolvedAt.HasValue) return true;
        if (!alert.IsOpen) return false;

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

    private static readonly HashSet<string> AllowedSeverities = ["Info", "Warning", "Critical"];
}
