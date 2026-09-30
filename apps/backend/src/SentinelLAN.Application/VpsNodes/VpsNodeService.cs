using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class VpsNodeService(
    IVpsNodeStore store,
    IVpsVaultService vaultService,
    IVpsSshService sshService)
{
    public async Task<IReadOnlyList<VpsNodeDto>> GetNodesAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        var nodes = await store.GetAllAsync(cancellationToken);
        return nodes.Select(ToDto).ToList();
    }

    public async Task<VpsNodeDto?> GetNodeByIdAsync(ActorContext actor, Guid id, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        var node = await store.GetByIdAsync(id, cancellationToken);
        return node is null ? null : ToDto(node);
    }

    public async Task<VpsNodeDto?> CreateNodeAsync(ActorContext actor, CreateVpsNodeRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 100) return null;
        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.Trim().Length is < 3 or > 255) return null;
        if (request.Port is < 1 or > 65535) return null;
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length is < 1 or > 64) return null;
        if (string.IsNullOrWhiteSpace(request.PrivateKey) || !request.PrivateKey.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase)) return null;
        if (!IsValidHostKeyFingerprint(request.HostKeyFingerprint)) return null;

        var name = request.Name.Trim();
        if (await store.ExistsNameAsync(name, null, cancellationToken)) return null;

        var encryptedKey = vaultService.Encrypt(request.PrivateKey.Trim());
        var node = new VpsNode
        {
            Name = name,
            Host = request.Host.Trim(),
            Port = request.Port,
            Username = request.Username.Trim(),
            EncryptedPrivateKey = encryptedKey,
            HostKeyFingerprint = request.HostKeyFingerprint.Trim(),
            Status = "Connecting"
        };

        var created = await store.CreateAsync(node, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "VpsNodeCreated",
            Reason = $"Created Cloud VPS Node '{created.Name}' ({created.Host}:{created.Port})",
            Outcome = "Success"
        }, cancellationToken);

        // Attempt initial connection probe asynchronously
        try
        {
            var testResult = await sshService.TestConnectionAsync(created.Host, created.Port, created.Username, request.PrivateKey.Trim(), created.HostKeyFingerprint!, cancellationToken);
            if (testResult.Success)
            {
                created.Status = "Online";
                created.OsInfo = testResult.OsInfo;
                created.Uptime = testResult.Uptime;
                created.CpuPercent = testResult.CpuPercent;
                created.RamPercent = testResult.RamPercent;
                created.DiskPercent = testResult.DiskPercent;
                created.DockerContainersCount = testResult.DockerContainersCount;
                created.LastCheckedAt = DateTimeOffset.UtcNow;
                created.ErrorMessage = null;
            }
            else
            {
                created.Status = "Error";
                created.ErrorMessage = testResult.Message;
                created.LastCheckedAt = DateTimeOffset.UtcNow;
            }
            await store.UpdateAsync(created, cancellationToken);
        }
        catch (Exception ex)
        {
            created.Status = "Error";
            created.ErrorMessage = ex.Message;
            created.LastCheckedAt = DateTimeOffset.UtcNow;
            await store.UpdateAsync(created, cancellationToken);
        }

        return ToDto(created);
    }

    public async Task<bool> DeleteNodeAsync(ActorContext actor, Guid id, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        var node = await store.GetByIdAsync(id, cancellationToken);
        if (node is null) return false;

        var deleted = await store.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            await store.RecordAuditAsync(new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                DeviceId = null,
                Action = "VpsNodeDeleted",
                Reason = $"Deleted Cloud VPS Node '{node.Name}' ({node.Host})",
                Outcome = "Success"
            }, cancellationToken);
        }
        return deleted;
    }

    public async Task<VpsConnectionTestResultDto> TestConnectionAsync(ActorContext actor, Guid id, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        var node = await store.GetByIdAsync(id, cancellationToken);
        if (node is null) return new VpsConnectionTestResultDto(false, "VPS Node not found");
        if (!IsValidHostKeyFingerprint(node.HostKeyFingerprint)) return new VpsConnectionTestResultDto(false, "SSH host key fingerprint is missing or invalid. Re-register this node with a verified SHA256 fingerprint.");

        string decryptedKey;
        try
        {
            decryptedKey = vaultService.Decrypt(node.EncryptedPrivateKey);
        }
        catch (Exception ex)
        {
            node.Status = "Error";
            node.ErrorMessage = "Failed to decrypt private key: " + ex.Message;
            node.LastCheckedAt = DateTimeOffset.UtcNow;
            await store.UpdateAsync(node, cancellationToken);
            return new VpsConnectionTestResultDto(false, node.ErrorMessage);
        }

        var result = await sshService.TestConnectionAsync(node.Host, node.Port, node.Username, decryptedKey, node.HostKeyFingerprint!, cancellationToken);
        node.Status = result.Success ? "Online" : "Error";
        node.ErrorMessage = result.Success ? null : result.Message;
        node.LastCheckedAt = DateTimeOffset.UtcNow;
        if (result.Success)
        {
            if (result.OsInfo is not null) node.OsInfo = result.OsInfo;
            if (result.Uptime is not null) node.Uptime = result.Uptime;
            if (result.CpuPercent.HasValue) node.CpuPercent = result.CpuPercent;
            if (result.RamPercent.HasValue) node.RamPercent = result.RamPercent;
            if (result.DiskPercent.HasValue) node.DiskPercent = result.DiskPercent;
            if (result.DockerContainersCount.HasValue) node.DockerContainersCount = result.DockerContainersCount;
        }
        await store.UpdateAsync(node, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "VpsNodeConnectionTested",
            Reason = $"Tested SSH connectivity to VPS '{node.Name}' ({node.Host}): {(result.Success ? "Connected" : result.Message)}",
            Outcome = result.Success ? "Success" : "Failed"
        }, cancellationToken);

        return result;
    }

    public async Task<VpsNodeDto?> RefreshMetricsAsync(ActorContext actor, Guid id, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        var node = await store.GetByIdAsync(id, cancellationToken);
        if (node is null) return null;
        if (!IsValidHostKeyFingerprint(node.HostKeyFingerprint))
        {
            node.Status = "Error";
            node.ErrorMessage = "SSH host key fingerprint is missing or invalid. Re-register this node with a verified SHA256 fingerprint.";
            await store.UpdateAsync(node, cancellationToken);
            return ToDto(node);
        }

        string decryptedKey;
        try
        {
            decryptedKey = vaultService.Decrypt(node.EncryptedPrivateKey);
        }
        catch (Exception ex)
        {
            node.Status = "Error";
            node.ErrorMessage = "Failed to decrypt private key: " + ex.Message;
            node.LastCheckedAt = DateTimeOffset.UtcNow;
            await store.UpdateAsync(node, cancellationToken);
            return ToDto(node);
        }

        var metrics = await sshService.CollectMetricsAsync(node.Host, node.Port, node.Username, decryptedKey, node.HostKeyFingerprint!, cancellationToken);
        node.LastCheckedAt = DateTimeOffset.UtcNow;
        if (metrics.Success)
        {
            node.Status = "Online";
            node.ErrorMessage = null;
            node.OsInfo = metrics.OsInfo;
            node.Uptime = metrics.Uptime;
            node.CpuPercent = metrics.CpuPercent;
            node.RamPercent = metrics.RamPercent;
            node.DiskPercent = metrics.DiskPercent;
            node.DockerContainersCount = metrics.DockerContainersCount;
        }
        else
        {
            node.Status = "Error";
            node.ErrorMessage = metrics.Message;
        }

        await store.UpdateAsync(node, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "VpsNodeMetricsRefreshed",
            Reason = $"Refreshed metrics for VPS '{node.Name}' (CPU: {node.CpuPercent}%, RAM: {node.RamPercent}%, Disk: {node.DiskPercent}%)",
            Outcome = metrics.Success ? "Success" : "Failed"
        }, cancellationToken);

        return ToDto(node) with { Runtime = metrics.Runtime };
    }

    public async Task<VpsCommandResultDto> RestartServiceAsync(ActorContext actor, Guid id, RestartVpsServiceRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        if (!request.Confirmed)
            return new VpsCommandResultDto(false, "Explicit confirmation is required");
        var now = DateTimeOffset.UtcNow;
        if (request.Nonce == Guid.Empty || request.ExpiresAt <= now || request.ExpiresAt > now.AddMinutes(5))
            return new VpsCommandResultDto(false, "A unique nonce and expiry within five minutes are required");
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            return new VpsCommandResultDto(false, "ServiceName is required");

        var serviceName = request.ServiceName.Trim();
        if (!VpsNode.IsAllowedService(serviceName))
        {
            return new VpsCommandResultDto(false, $"Service '{serviceName}' is not in the allow-list. Allowed services: {string.Join(", ", VpsNode.GetAllowedServices())}");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 1000)
            return new VpsCommandResultDto(false, "Reason must be 3-1000 characters");

        var node = await store.GetByIdAsync(id, cancellationToken);
        if (node is null) return new VpsCommandResultDto(false, "VPS Node not found");
        if (!IsValidHostKeyFingerprint(node.HostKeyFingerprint)) return new VpsCommandResultDto(false, "SSH host key fingerprint is missing or invalid. Re-register this node with a verified SHA256 fingerprint.");

        string decryptedKey;
        try
        {
            decryptedKey = vaultService.Decrypt(node.EncryptedPrivateKey);
        }
        catch (Exception ex)
        {
            return new VpsCommandResultDto(false, "Failed to decrypt private key: " + ex.Message);
        }

        if (!await store.TryReserveActionAsync(new VpsActionReservation
        {
            VpsNodeId = node.Id,
            ActorId = actor.UserId,
            Nonce = request.Nonce,
            ExpiresAt = request.ExpiresAt
        }, cancellationToken))
            return new VpsCommandResultDto(false, "This restart request has already been used");

        var result = await sshService.RestartServiceAsync(node.Host, node.Port, node.Username, decryptedKey, node.HostKeyFingerprint!, serviceName, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "VpsServiceRestarted",
            Reason = $"Restarted service '{serviceName}' on VPS '{node.Name}' ({node.Host}). Nonce: {request.Nonce}. Reason: {request.Reason.Trim()}",
            Outcome = result.Success ? "Success" : "Failed"
        }, cancellationToken);

        return result;
    }

    public async Task<VpsCommandResultDto> ExecuteOperationAsync(ActorContext actor, Guid id, VpsOperationRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        var now = DateTimeOffset.UtcNow;
        if (!request.Confirmed || request.Reason?.Trim().Length is not (>= 3 and <= 1000) ||
            request.Nonce == Guid.Empty || request.ExpiresAt <= now || request.ExpiresAt > now.AddMinutes(5))
            return new(false, "Cần lý do, xác nhận, nonce mới và thời hạn tối đa 5 phút.");
        if (!request.HasAllowedCommand()) return new(false, "Thao tác, đối tượng hoặc giá trị không thuộc danh sách cho phép.");
        var node = await store.GetByIdAsync(id, cancellationToken);
        if (node is null) return new(false, "Không tìm thấy VPS.");
        if (!IsValidHostKeyFingerprint(node.HostKeyFingerprint)) return new(false, "Cần xác minh fingerprint SSH của VPS.");
        string key;
        try { key = vaultService.Decrypt(node.EncryptedPrivateKey); }
        catch (Exception) { return new(false, "Không giải mã được khóa SSH."); }
        if (!await store.TryReserveActionAsync(new VpsActionReservation
        {
            VpsNodeId = id, ActorId = actor.UserId, Nonce = request.Nonce, ExpiresAt = request.ExpiresAt
        }, cancellationToken)) return new(false, "Yêu cầu này đã được sử dụng.");

        // Persist intent before a command can reboot the machine hosting this API.
        var reason = $"VPS {node.Id} ({node.Name}); action={request.Action}; target={request.Target}; value={request.Value}; nonce={request.Nonce}; expires={request.ExpiresAt:O}; {request.Reason.Trim()}";
        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId, ActorId = actor.UserId,
            Action = "VpsOperationRequested", Reason = reason, Outcome = "Accepted"
        }, cancellationToken);
        VpsCommandResultDto result;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ExpiresAt <= DateTimeOffset.UtcNow) result = new(false, "Yêu cầu đã hết hạn trước khi thực thi.");
            else result = await sshService.ExecuteOperationAsync(node.Host, node.Port, node.Username, key, node.HostKeyFingerprint!, request, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { result = new(false, "Không xác minh được kết quả lệnh SSH. Kiểm tra VPS trước khi gửi yêu cầu mới."); }
        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId, ActorId = actor.UserId,
            Action = "VpsOperationCompleted", Reason = reason,
            Outcome = result.Success ? (request.Action == "Reboot" ? "Scheduled" : "Success") : "Failed"
        }, cancellationToken);
        return result;
    }

    private static bool IsValidHostKeyFingerprint(string? value)
    {
        if (value is null || !value.StartsWith("SHA256:", StringComparison.Ordinal)) return false;
        var fingerprint = value[7..];
        if (fingerprint.Length != 43) return false;
        try { return Convert.FromBase64String(fingerprint + "=").Length == 32; }
        catch (FormatException) { return false; }
    }

    private static VpsNodeDto ToDto(VpsNode node) => new(
        node.Id,
        node.Name,
        node.Host,
        node.Port,
        node.Username,
        node.HostKeyFingerprint,
        node.Status,
        node.CpuPercent,
        node.RamPercent,
        node.DiskPercent,
        node.DockerContainersCount,
        node.Uptime,
        node.OsInfo,
        node.LastCheckedAt,
        node.ErrorMessage,
        node.CreatedAt,
        node.UpdatedAt
    );
}
