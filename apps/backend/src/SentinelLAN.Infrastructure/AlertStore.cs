using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AlertStore(SentinelDbContext db, IAlertDeviceLookup deviceLookup) : IAlertStore
{
    public async Task<IReadOnlyList<AlertDto>> GetAlertsAsync(Guid organizationId, bool? onlyOpen, CancellationToken cancellationToken)
    {
        var query = db.Alerts
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId);

        if (onlyOpen == true) query = query.Where(a => a.IsOpen);
        else if (onlyOpen == false) query = query.Where(a => !a.IsOpen);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var deviceIds = alerts.Where(a => a.DeviceId.HasValue).Select(a => a.DeviceId!.Value).Distinct().ToList();
        var deviceNames = await deviceLookup.GetDeviceNamesAsync(organizationId, deviceIds, cancellationToken);

        return alerts.Select(a => new AlertDto(
            a.Id,
            a.DeviceId,
            a.DeviceId.HasValue ? deviceNames.GetValueOrDefault(a.DeviceId.Value) : null,
            a.Severity,
            a.Message,
            a.IsOpen,
            a.AcknowledgedAt,
            a.ResolvedAt,
            a.CreatedAt
        )).ToList();
    }

    public Task<Alert?> FindAlertAsync(Guid organizationId, Guid alertId, CancellationToken cancellationToken) =>
        db.Alerts.SingleOrDefaultAsync(a => a.OrganizationId == organizationId && a.Id == alertId, cancellationToken);

    public void AddAlert(Alert alert) => db.Alerts.Add(alert);

    public void AddAudit(AuditLog log) => db.AuditLogs.Add(log);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
