using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IQrManagementService
{
    Task<(ManagementResultStatus Status, QrLabelResponse? Label, string Message)> GenerateOrRotateLabelAsync(
        ActorContext actor, Guid deviceId, GenerateQrLabelRequest request, CancellationToken cancellationToken = default);

    Task<(ManagementResultStatus Status, string Message)> RevokeLabelAsync(
        ActorContext actor, Guid deviceId, RevokeQrLabelRequest request, CancellationToken cancellationToken = default);

    Task<QrLabelStatusDto?> GetActiveLabelAsync(
        ActorContext actor, Guid deviceId, CancellationToken cancellationToken = default);

    Task<PublicQrResolveDto?> ResolvePublicAsync(
        string code, CancellationToken cancellationToken = default);

    Task<(ManagementResultStatus Status, AuthenticatedQrResolveDto? Result, string Message)> ResolveAuthenticatedAsync(
        ActorContext actor, string code, CancellationToken cancellationToken = default);
}

public sealed class QrManagementService(
    IManagementStore store,
    IQrCodeGenerator? qrGenerator = null,
    ISecretHasher? secretHasher = null) : IQrManagementService
{
    private readonly IQrCodeGenerator qrGenerator = qrGenerator ?? new DefaultQrCodeGenerator();
    private readonly ISecretHasher secretHasher = secretHasher ?? new DefaultSecretHasher();

    public async Task<(ManagementResultStatus Status, QrLabelResponse? Label, string Message)> GenerateOrRotateLabelAsync(
        ActorContext actor, Guid deviceId, GenerateQrLabelRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role is not (Roles.Admin or Roles.Technician))
            return (ManagementResultStatus.Forbidden, null, "Only administrators and technicians can manage QR labels.");

        if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 1000)
            return (ManagementResultStatus.Invalid, null, "A reason (3-1000 chars) and confirmation are required.");

        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null)
            return (ManagementResultStatus.NotFound, null, "Device not found.");

        if (device.IsRevoked)
            return (ManagementResultStatus.Conflict, null, "Cannot generate QR label for a revoked device.");

        // Revoke any existing active label for this device
        var existingLabel = await store.FindActiveQrLabelByDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        existingLabel?.Revoke(DateTimeOffset.UtcNow);

        var rawCode = qrGenerator.GenerateOpaqueCode();
        var codeHash = secretHasher.Create(rawCode);
        var prefix = $"qr-{rawCode[..Math.Min(8, rawCode.Length)]}";
        var expiresAt = request.ValidForDays.HasValue ? DateTimeOffset.UtcNow.AddDays(Math.Clamp(request.ValidForDays.Value, 1, 365)) : (DateTimeOffset?)null;

        var label = new DeviceQrLabel
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = device.Id,
            CodeHash = codeHash,
            CodePrefix = prefix,
            ExpiresAt = expiresAt
        };
        store.AddQrLabel(label);

        // Security rule: NEVER store raw code in audit log!
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "DeviceQrLabelGenerated",
            Reason = $"{request.Reason.Trim()} (label prefix {prefix})",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken))
            return (ManagementResultStatus.Conflict, null, "Concurrency conflict while generating QR label.");

        var response = new QrLabelResponse(label.Id, rawCode, prefix, $"/qr/{rawCode}", label.CreatedAt, expiresAt);
        return (ManagementResultStatus.Succeeded, response, "QR label generated successfully.");
    }

    public async Task<(ManagementResultStatus Status, string Message)> RevokeLabelAsync(
        ActorContext actor, Guid deviceId, RevokeQrLabelRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role is not (Roles.Admin or Roles.Technician))
            return (ManagementResultStatus.Forbidden, "Only administrators and technicians can revoke QR labels.");

        if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 1000)
            return (ManagementResultStatus.Invalid, "Reason and confirmation are required.");

        var label = await store.FindActiveQrLabelByDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (label is null)
            return (ManagementResultStatus.NotFound, "No active QR label found for this device.");

        label.Revoke(DateTimeOffset.UtcNow);

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = deviceId,
            Action = "DeviceQrLabelRevoked",
            Reason = $"{request.Reason.Trim()} (label prefix {label.CodePrefix})",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken))
            return (ManagementResultStatus.Conflict, "Concurrency conflict while revoking QR label.");

        return (ManagementResultStatus.Succeeded, "QR label revoked successfully.");
    }

    public async Task<QrLabelStatusDto?> GetActiveLabelAsync(
        ActorContext actor, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var label = await store.FindActiveQrLabelByDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (label is null) return null;
        return new QrLabelStatusDto(label.Id, label.CodePrefix, label.CreatedAt, label.ExpiresAt, label.LastScannedAt, label.IsActive(DateTimeOffset.UtcNow));
    }

    public async Task<PublicQrResolveDto?> ResolvePublicAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var codeHash = secretHasher.Create(code.Trim());
        var label = await store.FindQrLabelByHashAsync(codeHash, cancellationToken);
        if (label is null || !label.IsActive(DateTimeOffset.UtcNow)) return null;

        var device = await store.FindDeviceAsync(label.OrganizationId, label.DeviceId, cancellationToken);
        if (device is null || device.IsRevoked) return null;

        // Privacy rule: Public response contains minimal field card without serial, assigned user, internal UUID, or telemetry.
        var assetTag = device.SerialNumber is not null && device.SerialNumber.Length > 0
            ? $"TAG-{device.SerialNumber[..Math.Min(8, device.SerialNumber.Length)]}"
            : $"TAG-{label.CodePrefix}";

        return new PublicQrResolveDto(
            device.Name,
            assetTag,
            device.AssetStatus,
            "Vui lòng liên hệ Quản trị viên IT để được hỗ trợ / Contact IT Administrator for support",
            device.IsOnline(DateTimeOffset.UtcNow),
            device.AssignedUserId.HasValue
        );
    }

    public async Task<(ManagementResultStatus Status, AuthenticatedQrResolveDto? Result, string Message)> ResolveAuthenticatedAsync(
        ActorContext actor, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (ManagementResultStatus.Invalid, null, "Code is required.");

        var codeHash = secretHasher.Create(code.Trim());
        var label = await store.FindTenantQrLabelByHashAsync(actor.OrganizationId, codeHash, cancellationToken);
        if (label is null || !label.IsActive(DateTimeOffset.UtcNow))
            return (ManagementResultStatus.NotFound, null, "QR label not found, expired, or revoked.");

        var device = await store.FindDeviceAsync(actor.OrganizationId, label.DeviceId, cancellationToken);
        if (device is null || device.IsRevoked)
            return (ManagementResultStatus.NotFound, null, "Device not found or has been revoked.");

        label.RecordScan(DateTimeOffset.UtcNow);

        // Security rule: Audit scan event using prefix and label ID, never raw code
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "DeviceQrScanned",
            Reason = $"QR label {label.Id} ({label.CodePrefix}) scanned by {actor.Role}",
            Outcome = "Success"
        });

        await store.TrySaveChangesAsync(cancellationToken);

        if (actor.Role is Roles.Admin or Roles.Technician)
        {
            return (ManagementResultStatus.Succeeded, new AuthenticatedQrResolveDto(
                device.Id,
                device.Name,
                $"/devices/{device.Id}",
                actor.Role,
                true
            ), "Authorized.");
        }

        if (actor.Role == Roles.Employee)
        {
            var isAssigned = device.AssignedUserId == actor.UserId;
            if (isAssigned)
            {
                return (ManagementResultStatus.Succeeded, new AuthenticatedQrResolveDto(
                    device.Id,
                    device.Name,
                    "/my-device",
                    Roles.Employee,
                    true
                ), "Authorized.");
            }

            return (ManagementResultStatus.Forbidden, null, "You are not authorized to access this QR label.");
        }

        return (ManagementResultStatus.Forbidden, null, "Role not authorized.");
    }
}
