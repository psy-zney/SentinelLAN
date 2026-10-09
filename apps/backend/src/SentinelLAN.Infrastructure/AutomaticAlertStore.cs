using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AutomaticAlertStore(SentinelDbContext db) : IAutomaticAlertStore
{
    public async Task<IReadOnlyList<Alert>> GetRecentAsync(Guid organizationId, Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken) =>
        await db.Alerts.Where(a => a.OrganizationId == organizationId && a.DeviceId == deviceId && a.AutomaticRule != null &&
            (a.IsOpen || a.CreatedAt >= since)).ToListAsync(cancellationToken);
    public void Add(Alert alert) => db.Alerts.Add(alert);
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
