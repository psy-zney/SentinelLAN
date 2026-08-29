using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class AuthCookieManager
{
    public const string AccessCookieName = "sentinellan.access";
    public const string RefreshCookieName = "sentinellan.refresh";
    public const string CsrfHeaderName = "X-SentinelLAN-CSRF";

    public void Write(HttpContext context, AuthenticationResult session)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Cookies.Append(AccessCookieName, session.AccessToken, CreateOptions(context, session.AccessTokenExpiresAt, "/"));
        context.Response.Cookies.Append(RefreshCookieName, session.RefreshToken, CreateOptions(context, session.RefreshTokenExpiresAt, "/api/v1/auth"));
    }

    public void Delete(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Cookies.Delete(AccessCookieName, CreateDeleteOptions(context, "/"));
        context.Response.Cookies.Delete(RefreshCookieName, CreateDeleteOptions(context, "/api/v1/auth"));
    }

    private static CookieOptions CreateOptions(HttpContext context, DateTimeOffset expiresAt, string path) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expiresAt,
        IsEssential = true
    };

    private static CookieOptions CreateDeleteOptions(HttpContext context, string path) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = path,
        IsEssential = true
    };
}
