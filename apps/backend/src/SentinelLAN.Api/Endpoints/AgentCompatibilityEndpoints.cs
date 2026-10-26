using System.Reflection;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class AgentCompatibilityEndpoints
{
    public static void MapAgentCompatibilityEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/agent/compatibility", (HttpContext http, ICommandSigner signer) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var assembly = typeof(AgentCompatibilityEndpoints).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return Results.Ok(new
            {
                version,
                commit = version?.Split('+').ElementAtOrDefault(1),
                commandKeyId = (signer as RsaCommandSigner)?.KeyId,
                commandPublicKeySha256 = (signer as RsaCommandSigner)?.PublicKeySha256,
                protocolVersion = 1,
                authentication = new[] { "hmac-sha256-v1" },
                features = new[] { "enrollment-recovery", "request-signing-upgrade", "heartbeat", "commands", "policy", "agent-wake" },
                exactVersionRequired = false
            });
        }).AllowAnonymous().RequireRateLimiting("sensitive")
            .WithSummary("Public Agent protocol and build information; no credentials or environment values")
            .WithDescription("Agent and server releases need not match. Enrollment and authenticated heartbeat require compatible request-signing contracts. Missing optional endpoints may be ignored; authentication failures must never downgrade to unsigned requests.");
    }
}
