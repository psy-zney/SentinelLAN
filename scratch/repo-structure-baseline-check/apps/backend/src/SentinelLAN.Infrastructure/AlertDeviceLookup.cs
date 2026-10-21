using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class AlertDeviceLookup(SentinelDbContext db) : IAlertDeviceLookup
{
    public Task<bool> DeviceExistsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.Devices.AnyAsync(device => device.OrganizationId == organizationId && device.Id == deviceId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDeviceNamesAsync(Guid organizationId, IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken)
    {
        if (deviceIds.Count == 0) return new Dictionary<Guid, string>();

        return await db.Devices
            .AsNoTracking()
            .Where(device => device.OrganizationId == organizationId && deviceIds.Contains(device.Id))
            .ToDictionaryAsync(device => device.Id, device => device.Name, cancellationToken);
    }
}
