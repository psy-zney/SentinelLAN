using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class DeviceManagementService(IManagementStore store)
{
    public async Task<DeviceManagementOutcome> AssignAsync(
        ActorContext actor, Guid deviceId, DeviceAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin) return new(null, ManagementResultStatus.Forbidden);
        if (!ManagementValidation.IsValid(request)) return new(null, ManagementResultStatus.Invalid);

        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return new(null, ManagementResultStatus.NotFound);
        if (device.IsRevoked) return new(null, ManagementResultStatus.Conflict);

        if (request.AssignedUserId is Guid assignedUserId)
        {
            var user = await store.FindUserAsync(actor.OrganizationId, assignedUserId, cancellationToken);
            if (user is null) return new(null, ManagementResultStatus.NotFound);
            if (user.Role != Roles.Employee) return new(null, ManagementResultStatus.Invalid);
            if (await store.HasActiveDeviceAssignmentAsync(actor.OrganizationId, assignedUserId, deviceId, cancellationToken))
                return new(null, ManagementResultStatus.Conflict);
        }

        device.AssignedUserId = request.AssignedUserId;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = request.AssignedUserId.HasValue ? "DeviceAssigned" : "DeviceUnassigned",
            Reason = request.Reason.Trim(),
            Outcome = "Success"
        });
        if (!await store.TrySaveChangesAsync(cancellationToken)) return new(null, ManagementResultStatus.Conflict);
        return new(ToDto(device), ManagementResultStatus.Succeeded);
    }

    public async Task<DeviceManagementOutcome> RevokeAsync(
        ActorContext actor, Guid deviceId, RevokeDeviceRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin) return new(null, ManagementResultStatus.Forbidden);
        if (!ManagementValidation.IsValid(request)) return new(null, ManagementResultStatus.Invalid);

        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return new(null, ManagementResultStatus.NotFound);
        if (!device.IsRevoked)
        {
            device.IsRevoked = true;
            device.AssignedUserId = null;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            var credential = await store.FindCredentialAsync(actor.OrganizationId, deviceId, cancellationToken);
            if (credential is not null) credential.RevokedAt ??= DateTimeOffset.UtcNow;
            store.AddAudit(new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                DeviceId = device.Id,
                Action = "DeviceRevoked",
                Reason = request.Reason.Trim(),
                Outcome = "Success"
            });
            if (!await store.TrySaveChangesAsync(cancellationToken)) return new(null, ManagementResultStatus.Conflict);
        }
        return new(ToDto(device), ManagementResultStatus.Succeeded);
    }

    private static DeviceDto ToDto(Device device) =>
        new(device.Id, device.Name, device.OsVersion, device.AgentVersion, device.LastSeenAt,
            device.IsOnline(DateTimeOffset.UtcNow), device.AssignedUserId, device.IsRevoked);
}
