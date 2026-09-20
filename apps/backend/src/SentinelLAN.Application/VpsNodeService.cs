using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class VpsNodeService(
    IVpsNodeStore store,
    IVpsVaultService vaultService,
    IVpsSshService sshService)
{
    public async Task<IReadOnlyList<VpsNodeDto>> GetNodesAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var nodes = await store.GetAllAsync(actor.OrganizationId, cancellationToken);
        return nodes.Select(ToDto).ToList();
    }

    public async Task<VpsNodeDto?> GetNodeByIdAsync(ActorContext actor, Guid id, CancellationToken cancellationToken)
    {
        var node = await store.GetByIdAsync(actor.OrganizationId, id, cancellationToken);
        return node is null ? null : ToDto(node);
    }

    public async Task<VpsNodeDto?> CreateNodeAsync(ActorContext actor, CreateVpsNodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 100) return null;
        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.Trim().Length is < 3 or > 255) return null;
        if (request.Port is < 1 or > 65535) return null;
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length is < 1 or > 64) return null;
        if (string.IsNullOrWhiteSpace(request.PrivateKey) || !request.PrivateKey.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase)) return null;

        var name = request.Name.Trim();
        if (await store.ExistsNameAsync(actor.OrganizationId, name, null, cancellationToken)) return null;

        var encryptedKey = vaultService.Encrypt(request.PrivateKey.Trim());
        var node = new VpsNode
        {
            OrganizationId = actor.OrganizationId,
            Name = name,
            Host = request.Host.Trim(),
            Port = request.Port,
            Username = request.Username.Trim(),
            EncryptedPrivateKey = encryptedKey,
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
            var testResult = await sshService.TestConnectionAsync(created.Host, created.Port, created.Username, request.PrivateKey.Trim(), cancellationToken);
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
        var node = await store.GetByIdAsync(actor.OrganizationId, id, cancellationToken);
        if (node is null) return false;

        var deleted = await store.DeleteAsync(actor.OrganizationId, id, cancellationToken);
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
        var node = await store.GetByIdAsync(actor.OrganizationId, id, cancellationToken);
        if (node is null) return new VpsConnectionTestResultDto(false, "VPS Node not found");

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

        var result = await sshService.TestConnectionAsync(node.Host, node.Port, node.Username, decryptedKey, cancellationToken);
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
        var node = await store.GetByIdAsync(actor.OrganizationId, id, cancellationToken);
        if (node is null) return null;

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

        var metrics = await sshService.CollectMetricsAsync(node.Host, node.Port, node.Username, decryptedKey, cancellationToken);
        node.LastCheckedAt = DateTimeOffset.UtcNow;
        if (metrics.Success)
        {
            node.Status = "Online";
            node.ErrorMessage = null;
            if (metrics.OsInfo is not null) node.OsInfo = metrics.OsInfo;
            if (metrics.Uptime is not null) node.Uptime = metrics.Uptime;
            if (metrics.CpuPercent.HasValue) node.CpuPercent = metrics.CpuPercent;
            if (metrics.RamPercent.HasValue) node.RamPercent = metrics.RamPercent;
            if (metrics.DiskPercent.HasValue) node.DiskPercent = metrics.DiskPercent;
            if (metrics.DockerContainersCount.HasValue) node.DockerContainersCount = metrics.DockerContainersCount;
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

        return ToDto(node);
    }

    public async Task<VpsCommandResultDto> RestartServiceAsync(ActorContext actor, Guid id, RestartVpsServiceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            return new VpsCommandResultDto(false, "ServiceName is required");

        var serviceName = request.ServiceName.Trim();
        if (!VpsNode.IsAllowedService(serviceName))
        {
            return new VpsCommandResultDto(false, $"Service '{serviceName}' is not in the allow-list. Allowed services: {string.Join(", ", VpsNode.GetAllowedServices())}");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3)
            return new VpsCommandResultDto(false, "Reason is required (minimum 3 characters)");

        var node = await store.GetByIdAsync(actor.OrganizationId, id, cancellationToken);
        if (node is null) return new VpsCommandResultDto(false, "VPS Node not found");

        string decryptedKey;
        try
        {
            decryptedKey = vaultService.Decrypt(node.EncryptedPrivateKey);
        }
        catch (Exception ex)
        {
            return new VpsCommandResultDto(false, "Failed to decrypt private key: " + ex.Message);
        }

        var result = await sshService.RestartServiceAsync(node.Host, node.Port, node.Username, decryptedKey, serviceName, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "VpsServiceRestarted",
            Reason = $"Restarted service '{serviceName}' on VPS '{node.Name}' ({node.Host}). Reason: {request.Reason.Trim()}",
            Outcome = result.Success ? "Success" : "Failed"
        }, cancellationToken);

        return result;
    }

    private static VpsNodeDto ToDto(VpsNode node) => new(
        node.Id,
        node.OrganizationId,
        node.Name,
        node.Host,
        node.Port,
        node.Username,
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
