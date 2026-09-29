using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class AlertEndpoints
{
    public static void MapAlertEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/alerts", async (HttpContext http, AlertService alertService, [FromQuery] bool? onlyOpen, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            return Results.Ok(await alertService.GetAlertsAsync(actor, onlyOpen, ct));
        }).RequireAuthorization(AuthorizationPolicies.ViewAlerts);

        v1.MapPost("/alerts", async (CreateAlertRequest request, HttpContext http, AlertService alertService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var alert = await alertService.TriggerAlertAsync(actor, request.DeviceId, request.Severity, request.Message, ct);
            if (alert is null) return Results.BadRequest();
            await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("alert-triggered", alert, ct);
            return Results.Created($"/api/v1/alerts/{alert.Id}", alert);
        }).RequireAuthorization(AuthorizationPolicies.ManageAlerts);

        v1.MapPost("/alerts/{id:guid}/acknowledge", async (Guid id, HttpContext http, AlertService alertService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var succeeded = await alertService.AcknowledgeAlertAsync(actor, id, ct);
            if (!succeeded) return Results.NotFound();
            await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("alert-updated", new { id, action = "acknowledged" }, ct);
            return Results.Ok(new { acknowledged = true });
        }).RequireAuthorization(AuthorizationPolicies.ManageAlerts);

        v1.MapPost("/alerts/{id:guid}/resolve", async (Guid id, HttpContext http, AlertService alertService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var succeeded = await alertService.ResolveAlertAsync(actor, id, ct);
            if (!succeeded) return Results.NotFound();
            await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("alert-updated", new { id, action = "resolved" }, ct);
            return Results.Ok(new { resolved = true });
        }).RequireAuthorization(AuthorizationPolicies.ManageAlerts);
    }
}
