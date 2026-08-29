using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class AgentAuthenticationDefaults
{
    public const string Scheme = "SentinelAgent";
    public const string DeviceIdHeader = "X-SentinelLAN-Device-Id";
    public const string DeviceSecretHeader = "X-SentinelLAN-Device-Secret";
}

public sealed class AgentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SentinelDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var deviceIdValue = Request.Headers[AgentAuthenticationDefaults.DeviceIdHeader].ToString();
        var secret = Request.Headers[AgentAuthenticationDefaults.DeviceSecretHeader].ToString();
        if (string.IsNullOrWhiteSpace(deviceIdValue) && string.IsNullOrWhiteSpace(secret))
            return AuthenticateResult.NoResult();

        if (!Guid.TryParse(deviceIdValue, out var deviceId) || string.IsNullOrWhiteSpace(secret))
            return AuthenticateResult.Fail("Both Agent identity headers are required.");

        var deviceIdentity = await (
            from credential in db.DeviceCredentials.AsNoTracking()
            join device in db.Devices.AsNoTracking() on credential.DeviceId equals device.Id
            where credential.DeviceId == deviceId &&
                  credential.RevokedAt == null &&
                  !device.IsRevoked &&
                  credential.OrganizationId == device.OrganizationId
            select new { credential.SecretHash, credential.OrganizationId })
            .SingleOrDefaultAsync(Context.RequestAborted);
        if (deviceIdentity is null || !SecretHash.Matches(secret, deviceIdentity.SecretHash))
            return AuthenticateResult.Fail("Invalid or revoked device credential.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, deviceId.ToString()),
            new Claim(SentinelAuthenticationDefaults.OrganizationClaim, deviceIdentity.OrganizationId.ToString()),
            new Claim(ClaimTypes.Role, Roles.Agent)
        };
        var identity = new ClaimsIdentity(claims, AgentAuthenticationDefaults.Scheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AgentAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}
