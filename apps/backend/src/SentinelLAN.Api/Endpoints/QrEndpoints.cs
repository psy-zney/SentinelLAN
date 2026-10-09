using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class QrEndpoints
{
    public static void MapQrEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/devices/{id:guid}/qr-label", async (Guid id, GenerateQrLabelRequest req, HttpContext http, SentinelDbContext db, IQrManagementService qrService, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var (status, label, message) = await qrService.GenerateOrRotateLabelAsync(actor, id, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Created($"/api/v1/devices/{id}/qr-label", label),
                ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "Device not found", Detail = message, Status = 404 }),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Conflict", Detail = message, Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
            };
        }).RequireAuthorization(AuthorizationPolicies.Admin).RequireRateLimiting("sensitive");

        v1.MapDelete("/devices/{id:guid}/qr-label", async (Guid id, [FromBody] RevokeQrLabelRequest? req, HttpContext http, SentinelDbContext db, IQrManagementService qrService, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var request = req ?? new RevokeQrLabelRequest("Revoked by technician", true);
            var (status, message) = await qrService.RevokeLabelAsync(actor, id, request, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.NoContent(),
                ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "Not found", Detail = message, Status = 404 }),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
            };
        }).RequireAuthorization(AuthorizationPolicies.Admin).RequireRateLimiting("sensitive");

        v1.MapGet("/devices/{id:guid}/qr-label", async (Guid id, HttpContext http, IQrManagementService qrService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var label = await qrService.GetActiveLabelAsync(actor, id, ct);
            return label is null ? Results.NotFound() : Results.Ok(label);
        }).RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapGet("/qr/{code}/public", async (string code, IQrManagementService qrService, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var info = await qrService.ResolvePublicAsync(code, ct);
            return info is null ? Results.NotFound() : Results.Ok(info);
        }).AllowAnonymous().RequireRateLimiting("sensitive");

        v1.MapGet("/qr/{code}", async (string code, HttpContext http, IQrManagementService qrService, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var (status, result, message) = await qrService.ResolveAuthenticatedAsync(actor, code, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(result),
                ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "QR not found", Detail = message, Status = 404 }),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid scan", Detail = message, Status = 400 })
            };
        }).RequireAuthorization().RequireRateLimiting("sensitive");
    }
}
