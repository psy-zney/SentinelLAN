using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class DeviceHealthReader(SentinelDbContext db) : IDeviceHealthReader
{
    public async Task<IReadOnlyList<Guid>> GetActiveOrganizationsAsync(CancellationToken cancellationToken) =>
        await db.Organizations.AsNoTracking().Where(o => !o.IsSuspended).Select(o => o.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceHealthObservation>> GetDevicesAsync(Guid organizationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now.AddMinutes(-32);
        var rows = await db.Devices.AsNoTracking().Where(d => d.OrganizationId == organizationId && !d.IsRevoked)
            .SelectMany(device => db.Telemetry.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.DeviceId == device.Id &&
                    (t.CollectedAt ?? t.CreatedAt) >= since && (t.CollectedAt ?? t.CreatedAt) <= now)
                .OrderByDescending(t => t.CollectedAt ?? t.CreatedAt).ThenByDescending(t => t.Id).Take(1024).DefaultIfEmpty(), (device, sample) => new
                {
                    device.Id,
                    device.Name,
                    device.LastSeenAt,
                    device.MaintenanceUntil,
                    Measurement = sample == null ? null : new HealthMeasurement(sample.CollectedAt ?? sample.CreatedAt,
                    sample.CpuPercent, sample.RamPercent, sample.DiskPercent, sample.ProtectionCollectedAt,
                    sample.AntivirusStatus, sample.FirewallDomainStatus, sample.FirewallPrivateStatus,
                    sample.FirewallPublicStatus, sample.DiskEncryptionStatus)
                }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.Id).Select(group =>
        {
            var device = group.First();
            return new DeviceHealthObservation(organizationId, device.Id, device.Name, device.LastSeenAt, device.MaintenanceUntil,
                group.Where(x => x.Measurement != null).Select(x => x.Measurement!).ToArray());
        }).ToArray();
    }
}
