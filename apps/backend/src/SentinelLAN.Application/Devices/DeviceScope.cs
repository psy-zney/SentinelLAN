using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class DeviceScope
{
    public static IQueryable<Device> ForActor(IQueryable<Device> devices, ActorContext actor) =>
        devices.Where(device =>
            device.OrganizationId == actor.OrganizationId &&
            (actor.Role != Roles.Employee || (device.AssignedUserId == actor.UserId && !device.IsRevoked)));
}
