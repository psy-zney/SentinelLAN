using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IEndpointObservationReader
{
    Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<Device?> FindAssignedDeviceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<TelemetrySnapshot?> LatestTelemetryAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
}

public sealed class EndpointObservationService(IEndpointObservationReader reader, TimeProvider clock)
{
    public async Task<EndpointObservationDto?> GetAsync(ActorContext actor, Guid? deviceId, CancellationToken ct)
    {
        if (actor.Role is not (Roles.Admin or Roles.Employee)) throw new UnauthorizedAccessException();
        var device = actor.Role == Roles.Employee
            ? await reader.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, ct)
            : deviceId is { } id ? await reader.FindDeviceAsync(actor.OrganizationId, id, ct) : null;
        if (device is null || device.IsRevoked || deviceId is { } requested && requested != device.Id) return null;
        var security = EndpointObservationMapping.Security(await reader.LatestTelemetryAsync(actor.OrganizationId, device.Id, ct));
        return new(EndpointObservationMapping.Inventory(device), security,
            security is null || security.CapturedAt < clock.GetUtcNow().AddMinutes(-2) || security.CapturedAt > clock.GetUtcNow().AddMinutes(1));
    }
}
