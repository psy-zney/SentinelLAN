using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class DeviceHealthReader(SentinelDbContext db) : IDeviceHealthReader
{
    public async Task<IReadOnlyList<Guid>> GetActiveOrganizationsAsync(CancellationToken cancellationToken) =>
        await db.Organizations.AsNoTracking().Where(o => !o.IsSuspended).Select(o => o.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceHealthObservation>> GetDevicesAsync(Guid organizationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var devices = await db.Devices.AsNoTracking().Where(d => d.OrganizationId == organizationId && !d.IsRevoked).ToListAsync(cancellationToken);
        var since = now.AddMinutes(-10);
        var observations = new List<DeviceHealthObservation>();
        foreach (var device in devices)
        {
            var samples = await db.Telemetry.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.DeviceId == device.Id &&
                    (t.CollectedAt ?? t.CreatedAt) >= since && (t.CollectedAt ?? t.CreatedAt) <= now)
                .OrderByDescending(t => t.CollectedAt ?? t.CreatedAt).Take(100)
                .Select(t => new HealthMeasurement(t.CollectedAt ?? t.CreatedAt, t.CpuPercent, t.RamPercent, t.DiskPercent)).ToListAsync(cancellationToken);
            observations.Add(new(organizationId, device.Id, device.Name, device.LastSeenAt, device.MaintenanceUntil, samples));
        }
        return observations;
    }
}
