using System.Security.Claims;
using SentinelLAN.Application;

namespace SentinelLAN.Api;

public static class ClaimsPrincipalExtensions
{
    public static ActorContext? ToActorContext(this ClaimsPrincipal principal)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var organizationIdValue = principal.FindFirstValue(SentinelAuthenticationDefaults.OrganizationClaim);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        return Guid.TryParse(userIdValue, out var userId) && Guid.TryParse(organizationIdValue, out var organizationId) && role is not null
            ? new ActorContext(userId, organizationId, role)
            : null;
    }

    public static AgentContext? ToAgentContext(this ClaimsPrincipal principal)
    {
        var deviceIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var organizationIdValue = principal.FindFirstValue(SentinelAuthenticationDefaults.OrganizationClaim);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        return role == Roles.Agent && Guid.TryParse(deviceIdValue, out var deviceId) && Guid.TryParse(organizationIdValue, out var organizationId)
            ? new AgentContext(deviceId, organizationId)
            : null;
    }
}
