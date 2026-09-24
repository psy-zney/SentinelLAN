using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SentinelLAN.Application;

namespace SentinelLAN.Api;

public static class SentinelAuthenticationDefaults
{
    public const string Scheme = "SentinelAccess";
    public const string OrganizationClaim = "sentinellan:organization";
}

public sealed class AccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAccessTokenService tokens,
    IAuthenticationStore users,
    TimeProvider timeProvider) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = GetToken();
        if (string.IsNullOrWhiteSpace(token)) return AuthenticateResult.NoResult();

        var actor = tokens.Validate(token, timeProvider.GetUtcNow());
        if (actor is null) return AuthenticateResult.Fail("Invalid or expired access token.");
        var user = await users.FindUserAsync(actor.Value.UserId, actor.Value.OrganizationId, Context.RequestAborted);
        if (user is null || user.Status != SentinelLAN.Domain.UserStatuses.Active || user.Role != actor.Value.Role ||
            user.SecurityStamp != actor.Value.SecurityStamp)
            return AuthenticateResult.Fail("Account is inactive or session was revoked.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actor.Value.UserId.ToString()),
            new Claim(SentinelAuthenticationDefaults.OrganizationClaim, actor.Value.OrganizationId.ToString()),
            new Claim(ClaimTypes.Role, actor.Value.Role)
        };
        var identity = new ClaimsIdentity(claims, SentinelAuthenticationDefaults.Scheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SentinelAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    private string? GetToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return authorization[7..].Trim();
        return Request.Cookies[AuthCookieManager.AccessCookieName];
    }
}
