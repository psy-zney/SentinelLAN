using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class HttpCurrentTenantProvider(IHttpContextAccessor httpContextAccessor) : ICurrentTenantProvider
{
    public Guid? CurrentOrganizationId => httpContextAccessor.HttpContext?.User?.ToActorContext()?.OrganizationId;
}
