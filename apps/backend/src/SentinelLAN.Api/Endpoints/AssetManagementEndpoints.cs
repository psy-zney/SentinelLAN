using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class AssetManagementEndpoints
{
    public static void MapAssetManagementEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/devices/{id:guid}/asset-detail", async (Guid id, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            if (http.User.ToActorContext() is not { } actor)
            {
                return Results.Unauthorized();
            }
            var detail = await assetService.GetDeviceAssetDetailAsync(actor, id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        }).RequireAuthorization();

        v1.MapPut("/devices/{id:guid}/asset-profile", async (Guid id, UpdateAssetProfileRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var (status, message) = await assetService.UpdateAssetProfileAsync(actor, id, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(new { message }),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid profile update", Detail = message })
            };
        }).RequireAuthorization(AuthorizationPolicies.Technician);

        v1.MapGet("/devices/{id:guid}/timeline", async (Guid id, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            if (http.User.ToActorContext() is not { } actor)
            {
                return Results.Unauthorized();
            }
            var timeline = await assetService.GetDeviceTimelineAsync(actor, id, ct);
            return Results.Ok(timeline);
        }).RequireAuthorization();

        v1.MapGet("/incidents", async (Guid? deviceId, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            if (http.User.ToActorContext() is not { } actor)
            {
                return Results.Unauthorized();
            }
            var incidents = await assetService.GetIncidentsAsync(actor, deviceId, ct);
            return Results.Ok(incidents);
        }).RequireAuthorization();

        v1.MapPost("/incidents", async (CreateIncidentRequest req, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            if (http.User.ToActorContext() is not { } actor)
            {
                return Results.Unauthorized();
            }
            var (status, incident, message) = await assetService.CreateIncidentAsync(actor, req with { IdempotencyKey = idempotencyKey }, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Created($"/api/v1/incidents/{incident!.Id}", incident),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Idempotency conflict", Detail = message, Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to report incident", Detail = message })
            };
        }).RequireAuthorization();

        v1.MapPut("/incidents/{id:guid}/status", async (Guid id, UpdateIncidentStatusRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var (status, message) = await assetService.UpdateIncidentStatusAsync(actor, id, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(new { message }),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to update incident", Detail = message })
            };
        }).RequireAuthorization(AuthorizationPolicies.Technician);

        v1.MapGet("/work-orders", async (Guid? deviceId, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            if (http.User.ToActorContext() is not { } actor)
            {
                return Results.Unauthorized();
            }
            var workOrders = await assetService.GetWorkOrdersAsync(actor, deviceId, ct);
            return Results.Ok(workOrders);
        }).RequireAuthorization();

        v1.MapPost("/work-orders", async (CreateWorkOrderRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var (status, wo, message) = await assetService.CreateWorkOrderAsync(actor, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Created($"/api/v1/work-orders/{wo!.Id}", wo),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to create work order", Detail = message })
            };
        }).RequireAuthorization(AuthorizationPolicies.Technician);

        v1.MapPut("/work-orders/{id:guid}/complete", async (Guid id, CompleteWorkOrderRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var (status, message) = await assetService.CompleteWorkOrderAsync(actor, id, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(new { message }),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to complete work order", Detail = message })
            };
        }).RequireAuthorization(AuthorizationPolicies.Technician);

        v1.MapGet("/asset-loans", async (Guid? deviceId, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var loans = await assetService.GetAssetLoansAsync(actor, deviceId, ct);
            return Results.Ok(loans);
        }).RequireAuthorization(AuthorizationPolicies.Technician);

        v1.MapPost("/asset-loans", async (CreateLoanRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var (status, loan, message) = await assetService.CreateAssetLoanAsync(actor, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Created($"/api/v1/asset-loans/{loan!.Id}", loan),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to create asset loan", Detail = message })
            };
        }).RequireAuthorization(AuthorizationPolicies.Technician);

        v1.MapPut("/asset-loans/{id:guid}/return", async (Guid id, ReturnLoanRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var (status, message) = await assetService.ReturnAssetLoanAsync(actor, id, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(new { message }),
                ManagementResultStatus.NotFound => Results.NotFound(new { message }),
                ManagementResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new ProblemDetails { Title = "Failed to process asset return", Detail = message })
            };
        }).RequireAuthorization(AuthorizationPolicies.Technician);
    }
}
