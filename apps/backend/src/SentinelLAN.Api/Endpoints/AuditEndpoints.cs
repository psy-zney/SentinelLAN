using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/audit-logs", async (HttpContext http, SentinelDbContext db, [FromQuery] Guid? deviceId, [FromQuery] string? action, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var query = db.AuditLogs.AsNoTracking().Where(x => x.OrganizationId == actor.OrganizationId);
            if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action.Trim()));

            var logs = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);

            var actorIds = logs.Select(x => x.ActorId).Distinct().ToList();
            var devIds = logs.Where(x => x.DeviceId.HasValue).Select(x => x.DeviceId!.Value).Distinct().ToList();

            var userNames = await db.Users.AsNoTracking().Where(u => u.OrganizationId == actor.OrganizationId && actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
            var deviceNames = await db.Devices.AsNoTracking().Where(d => d.OrganizationId == actor.OrganizationId && (devIds.Contains(d.Id) || actorIds.Contains(d.Id))).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

            return Results.Ok(logs.Select(x => new AuditLogDto(
                x.Id,
                x.ActorId,
                userNames.GetValueOrDefault(x.ActorId) ?? deviceNames.GetValueOrDefault(x.ActorId) ?? "System",
                x.DeviceId,
                x.DeviceId.HasValue ? deviceNames.GetValueOrDefault(x.DeviceId.Value) : null,
                x.Action,
                x.Reason,
                x.Outcome,
                x.CreatedAt
            )));
        }).RequireAuthorization(AuthorizationPolicies.ViewAudit);
    }
}
