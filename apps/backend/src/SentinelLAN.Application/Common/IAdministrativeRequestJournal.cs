using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IAdministrativeRequestJournal
{
    Task<bool> NonceExistsAsync(Guid organizationId, Guid nonce, CancellationToken cancellationToken);
    void Reserve(AdministrativeRequestNonce nonce);
    void Audit(AuditLog audit);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}
