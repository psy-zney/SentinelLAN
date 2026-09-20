namespace SentinelLAN.Application;

public sealed record VpsNodeDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Host,
    int Port,
    string Username,
    string Status,
    double? CpuPercent,
    double? RamPercent,
    double? DiskPercent,
    int? DockerContainersCount,
    string? Uptime,
    string? OsInfo,
    DateTimeOffset? LastCheckedAt,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateVpsNodeRequest(
    string Name,
    string Host,
    int Port,
    string Username,
    string PrivateKey
);

public sealed record UpdateVpsNodeRequest(
    string Name,
    string Host,
    int Port,
    string Username,
    string? NewPrivateKey = null
);

public sealed record RestartVpsServiceRequest(
    string ServiceName,
    string Reason
);

public sealed record VpsConnectionTestResultDto(
    bool Success,
    string Message,
    string? OsInfo = null,
    string? Uptime = null,
    double? CpuPercent = null,
    double? RamPercent = null,
    double? DiskPercent = null,
    int? DockerContainersCount = null
);

public sealed record VpsMetricsResultDto(
    bool Success,
    string Message,
    string? OsInfo = null,
    string? Uptime = null,
    double? CpuPercent = null,
    double? RamPercent = null,
    double? DiskPercent = null,
    int? DockerContainersCount = null
);

public sealed record VpsCommandResultDto(
    bool Success,
    string Message,
    string? Output = null
);

public interface IVpsVaultService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public interface IVpsSshService
{
    Task<VpsConnectionTestResultDto> TestConnectionAsync(string host, int port, string username, string decryptedPrivateKey, CancellationToken cancellationToken = default);
    Task<VpsMetricsResultDto> CollectMetricsAsync(string host, int port, string username, string decryptedPrivateKey, CancellationToken cancellationToken = default);
    Task<VpsCommandResultDto> RestartServiceAsync(string host, int port, string username, string decryptedPrivateKey, string serviceName, CancellationToken cancellationToken = default);
}

public interface IVpsNodeStore
{
    Task<IReadOnlyList<Domain.VpsNode>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Domain.VpsNode?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default);
    Task<Domain.VpsNode> CreateAsync(Domain.VpsNode node, CancellationToken cancellationToken = default);
    Task<Domain.VpsNode> UpdateAsync(Domain.VpsNode node, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsNameAsync(Guid organizationId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task RecordAuditAsync(Domain.AuditLog auditLog, CancellationToken cancellationToken = default);
}
