using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IAutomaticAlertStore
{
    Task<IReadOnlyList<Alert>> GetRecentAsync(Guid organizationId, Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken);
    void Add(Alert alert);
    void AddAudit(AuditLog audit);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
