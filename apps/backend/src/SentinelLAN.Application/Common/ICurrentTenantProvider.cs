using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface ICurrentTenantProvider
{
    Guid? CurrentOrganizationId { get; }
}
