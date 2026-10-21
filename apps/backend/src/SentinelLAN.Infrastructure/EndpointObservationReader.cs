using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class EndpointObservationReader(SentinelDbContext db, TimeProvider clock) : IEndpointObservationReader
{
    public Task<Device?> FindDeviceAsync(Guid org, Guid device, CancellationToken ct) =>
        db.Devices.AsNoTracking().SingleOrDefaultAsync(value => value.OrganizationId == org && value.Id == device, ct);
    public Task<Device?> FindAssignedDeviceAsync(Guid org, Guid user, CancellationToken ct) =>
        db.Devices.AsNoTracking().FirstOrDefaultAsync(value => value.OrganizationId == org && value.AssignedUserId == user && !value.IsRevoked, ct);
    public Task<TelemetrySnapshot?> LatestTelemetryAsync(Guid org, Guid device, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.Telemetry.AsNoTracking().Where(value => value.OrganizationId == org && value.DeviceId == device &&
                (value.CollectedAt ?? value.CreatedAt) >= now.AddDays(-30) && (value.CollectedAt ?? value.CreatedAt) <= now)
            .OrderByDescending(value => value.CollectedAt ?? value.CreatedAt).FirstOrDefaultAsync(ct);
    }
}
