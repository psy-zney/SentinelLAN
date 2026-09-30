namespace SentinelLAN.Application;

public sealed record VpsNodeDto(
    Guid Id,
    string Name,
    string Host,
    int Port,
    string Username,
    string? HostKeyFingerprint,
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
    DateTimeOffset UpdatedAt,
    VpsRuntimeDto? Runtime = null
);

public sealed record CreateVpsNodeRequest(
    string Name,
    string Host,
    int Port,
    string Username,
    string PrivateKey,
    string HostKeyFingerprint
);

public sealed record RestartVpsServiceRequest(
    string ServiceName,
    string Reason,
    bool Confirmed,
    Guid Nonce,
    DateTimeOffset ExpiresAt
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
    int? DockerContainersCount = null,
    VpsRuntimeDto? Runtime = null
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
    Task<VpsConnectionTestResultDto> TestConnectionAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default);
    Task<VpsMetricsResultDto> CollectMetricsAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default);
    Task<VpsCommandResultDto> RestartServiceAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, string serviceName, CancellationToken cancellationToken = default);
    Task<VpsCommandResultDto> ExecuteOperationAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, VpsOperationRequest request, CancellationToken cancellationToken = default);
}

public interface IVpsNodeStore
{
    Task<IReadOnlyList<Domain.VpsNode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Domain.VpsNode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Domain.VpsNode> CreateAsync(Domain.VpsNode node, CancellationToken cancellationToken = default);
    Task<Domain.VpsNode> UpdateAsync(Domain.VpsNode node, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task RecordAuditAsync(Domain.AuditLog auditLog, CancellationToken cancellationToken = default);
    Task<bool> TryReserveActionAsync(Domain.VpsActionReservation reservation, CancellationToken cancellationToken = default);
}
