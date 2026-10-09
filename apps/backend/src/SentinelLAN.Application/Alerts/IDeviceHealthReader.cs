namespace SentinelLAN.Application;

public interface IDeviceHealthReader
{
    Task<IReadOnlyList<Guid>> GetActiveOrganizationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceHealthObservation>> GetDevicesAsync(Guid organizationId, DateTimeOffset now, CancellationToken cancellationToken);
}
