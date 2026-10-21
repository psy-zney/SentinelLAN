using System.Security.Cryptography;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public sealed record UpgradeAgentRequestSigningRequest(Guid DeviceId, string DeviceSecret);
public sealed record UpgradeAgentRequestSigningResponse(string RequestSigningKey);

public static class AgentAuthenticationEndpoints
{
    public static void MapAgentAuthenticationEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/agent/auth/upgrade", async (UpgradeAgentRequestSigningRequest input, HttpContext http, AgentCredentialProvisioner provisioner, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (input.DeviceId == Guid.Empty || input.DeviceSecret is not { Length: 64 } || !input.DeviceSecret.All(char.IsAsciiHexDigit)) return Results.Unauthorized();
            try
            {
                var key = await provisioner.ProvisionAsync(input.DeviceId, input.DeviceSecret, ct);
                return key is null ? Results.Unauthorized() : Results.Ok(new UpgradeAgentRequestSigningResponse(key));
            }
            catch (CryptographicException) { return Results.Unauthorized(); }
        }).RequireRateLimiting("sensitive").WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2048))
            .Produces<UpgradeAgentRequestSigningResponse>()
            .WithDescription("One-time TLS bootstrap for an existing device credential. Returns a dedicated request-signing key; repeated proofs recover the same key. Normal Agent requests require HMAC, timestamp and a one-use nonce outside Development or an explicitly bounded migration window.");
    }
}
