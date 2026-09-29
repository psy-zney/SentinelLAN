using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class MyDeviceEndpoints
{
    public static void MapMyDeviceEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/my-device", async (HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var device = await myDeviceService.GetMyDeviceAsync(actor, ct);
            return device is null ? Results.NotFound() : Results.Ok(device);
        }).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

        v1.MapGet("/my-device/telemetry", async ([FromQuery] int? limit, HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var history = await myDeviceService.GetMyDeviceTelemetryAsync(actor, limit ?? 10, ct);
            return Results.Ok(history);
        }).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

        v1.MapGet("/my-device/incidents", async (HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var incidents = await myDeviceService.GetMyDeviceIncidentsAsync(actor, ct);
            return Results.Ok(incidents);
        }).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

        v1.MapPost("/my-device/incidents", async (ReportMyDeviceIncidentRequest req, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var (status, incident, message) = await myDeviceService.ReportIncidentAsync(actor, req with { IdempotencyKey = idempotencyKey }, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Created($"/api/v1/my-device/incidents/{incident!.Id}", incident),
                ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "Device not found", Detail = message, Status = 404 }),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Idempotency conflict", Detail = message, Status = 409 }),
                ManagementResultStatus.Invalid => Results.BadRequest(new ProblemDetails { Title = "Invalid incident request", Detail = message, Status = 400 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to report incident", Detail = message, Status = 400 })
            };
        }).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");
    }
}
