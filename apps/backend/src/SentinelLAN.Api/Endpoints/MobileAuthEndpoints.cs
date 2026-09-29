using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class MobileAuthEndpoints
{
    public static void MapMobileAuthEndpoints(this RouteGroupBuilder v1)
    {
        var mobile = v1.MapGroup("/mobile");
        var mobileAuth = mobile.MapGroup("/auth");

        mobileAuth.MapPost("/login", async (MobileLoginRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var result = await authentication.LoginMobileAsync(request, ct);
            if (result is null) return Results.Unauthorized();
            return Results.Ok(new MobileAuthSessionResponse(
                result.AccessToken,
                (int)(result.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
                result.RefreshToken,
                result.RefreshTokenExpiresAt,
                "Bearer",
                new MobileUserInfo(result.UserId, result.Email, result.DisplayName, result.Role, result.OrganizationId, result.OrganizationCode)
            ));
        }).AllowAnonymous().RequireRateLimiting("sensitive");

        mobileAuth.MapPost("/refresh", async (MobileRefreshRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Results.Unauthorized();
            var result = await authentication.RefreshMobileAsync(request.RefreshToken, ct);
            if (result.Status != RefreshStatus.Succeeded || result.Result is null)
            {
                return Results.Unauthorized();
            }
            return Results.Ok(new MobileRefreshResponse(
                result.Result.AccessToken,
                (int)(result.Result.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
                result.Result.RefreshToken,
                result.Result.RefreshTokenExpiresAt,
                "Bearer"
            ));
        }).AllowAnonymous().RequireRateLimiting("sensitive");

        mobileAuth.MapPost("/logout", async (MobileLogoutRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            await authentication.LogoutMobileAsync(request.RefreshToken, ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("sensitive");

        mobileAuth.MapPost("/logout-all", async (HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            await authentication.LogoutAllUserSessionsAsync(actor.OrganizationId, actor.UserId, ct);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("sensitive");

        mobile.MapGet("/bootstrap", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            return Results.Ok(new MobileBootstrapResponse(
                MinimumAppVersion: "1.0.0",
                LatestAppVersion: "1.0.0",
                PrivacyManifestVersion: "2026.1",
                MaintenanceMode: false,
                SupportEmail: "it-support@sentinellan.local",
                SupportedAuthSchemes: ["Bearer"]
            ));
        }).AllowAnonymous();
    }
}
