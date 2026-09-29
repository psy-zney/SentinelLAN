using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/auth/login", async (LoginRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, AuthCookieManager cookies, CancellationToken ct) =>
        {
            var session = await authentication.LoginAsync(request, ct);
            if (session is null) return Results.Unauthorized();
            cookies.Write(http, session);
            return Results.Ok(new AuthSessionResponse((int)(session.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, session.Role, session.DisplayName));
        }).RequireRateLimiting("sensitive");

        v1.MapPost("/auth/refresh", async (HttpContext http, SentinelLAN.Application.AuthenticationService authentication, AuthCookieManager cookies, CancellationToken ct) =>
        {
            var result = await authentication.RefreshAsync(http.Request.Cookies[AuthCookieManager.RefreshCookieName], ct);
            if (result.Status != RefreshStatus.Succeeded || result.Session is null)
            {
                cookies.Delete(http);
                return Results.Unauthorized();
            }
            cookies.Write(http, result.Session);
            return Results.Ok(new AuthSessionResponse((int)(result.Session.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, result.Session.Role, result.Session.DisplayName));
        }).RequireRateLimiting("sensitive");

        v1.MapPost("/auth/logout", async (HttpContext http, SentinelLAN.Application.AuthenticationService authentication, AuthCookieManager cookies, CancellationToken ct) =>
        {
            await authentication.LogoutAsync(http.Request.Cookies[AuthCookieManager.RefreshCookieName], ct);
            cookies.Delete(http);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        v1.MapGet("/auth/session", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var user = await db.Users
                .AsNoTracking()
                .SingleAsync(item => item.Id == actor.UserId && item.OrganizationId == actor.OrganizationId, ct);
            return Results.Ok(new CurrentSessionResponse(user.Role, user.DisplayName));
        }).RequireAuthorization();

        v1.MapPost("/auth/activation/validate", async (ValidateActivationTokenRequest req, UserManagementService users, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var result = await users.ValidateActivationTokenAsync(req.Token, ct);
            return Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting("sensitive");

        v1.MapPost("/auth/activate", async (ActivateAccountRequest req, UserManagementService users, IPasswordHasher passwordHasher, SentinelDbContext db, HttpContext http, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var (status, message) = await users.ActivateAccountAsync(req, passwordHasher, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(new { message }),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Activation conflict", Detail = message, Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Activation failed", Detail = message, Status = 400 })
            };
        }).AllowAnonymous().RequireRateLimiting("sensitive");
    }
}
