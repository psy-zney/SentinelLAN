using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPut("/devices/{id:guid}/assignment", async (Guid id, DeviceAssignmentRequest request, HttpContext http, SentinelDbContext db, DeviceManagementService devices, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var result = await devices.AssignAsync(actor, id, request, ct);
            return result.Status switch
            {
                ManagementResultStatus.Succeeded => await PublishDeviceUpdateAsync(result.Device!, hub, http, ct),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ManagementResultStatus.NotFound => Results.NotFound(),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Device assignment conflicts with an existing active assignment", Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid assignment details", Status = 400 })
            };
        })
            .WithName("AssignDevice")
            .Produces<DeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapPost("/devices/{id:guid}/revoke", async (Guid id, RevokeDeviceRequest request, HttpContext http, SentinelDbContext db, DeviceManagementService devices, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var result = await devices.RevokeAsync(actor, id, request, ct);
            return result.Status switch
            {
                ManagementResultStatus.Succeeded => await PublishDeviceUpdateAsync(result.Device!, hub, http, ct),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ManagementResultStatus.NotFound => Results.NotFound(),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Device could not be revoked", Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Confirmation and a valid reason are required", Status = 400 })
            };
        })
            .WithName("RevokeDevice")
            .Produces<DeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapGet("/devices", async (int? offset, int? limit, string? q, string? status, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var now = DateTimeOffset.UtcNow;
            var page = new PageWindow(offset ?? 0, limit ?? 200);
            page.Validate();
            if (q?.Length > 100) return Results.BadRequest();
            if (status is not (null or "all" or "online" or "offline" or "revoked")) return Results.BadRequest();
            var query = DeviceScope.ForActor(db.Devices.AsNoTracking(), actor);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim().ToLowerInvariant();
                // EF translates this expression to SQL LOWER; invariant/comparison overloads cannot be translated by Npgsql.
#pragma warning disable CA1304, CA1311, CA1862
                query = query.Where(x => (x.Name + " " + x.OsVersion).ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
            }
            var cutoff = now - Device.OnlineTimeout;
            if (status == "online") query = query.Where(x => !x.IsRevoked && x.LastSeenAt != null && x.LastSeenAt > cutoff);
            if (status == "offline") query = query.Where(x => !x.IsRevoked && (x.LastSeenAt == null || x.LastSeenAt <= cutoff));
            if (status == "revoked") query = query.Where(x => x.IsRevoked);
            var devices = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
            return Results.Ok(devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now), x.AssignedUserId, x.IsRevoked)));
        }).RequireAuthorization(AuthorizationPolicies.ViewDevices);

        v1.MapGet("/devices/{id:guid}", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var device = await DeviceScope.ForActor(db.Devices, actor).SingleOrDefaultAsync(x => x.Id == id, ct);
            return device is null ? Results.NotFound() : Results.Ok(new DeviceDto(device.Id, device.Name, device.OsVersion, device.AgentVersion, device.LastSeenAt, device.IsOnline(DateTimeOffset.UtcNow), device.AssignedUserId, device.IsRevoked));
        }).RequireAuthorization();

        v1.MapGet("/devices/{id:guid}/telemetry", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var canAccess = await DeviceScope.ForActor(db.Devices, actor).AnyAsync(x => x.Id == id, ct);
            if (!canAccess) return Results.NotFound();
            var snapshots = await db.Telemetry
                .Where(x => x.DeviceId == id && x.OrganizationId == actor.OrganizationId)
                .Where(x => (x.CollectedAt ?? x.CreatedAt) >= DateTimeOffset.UtcNow.AddDays(-TechnicalDataRetentionService.RetentionDays))
                .OrderByDescending(x => x.CollectedAt ?? x.CreatedAt)
                .Take(100)
                .Select(x => new TelemetrySnapshotDto(x.Id, x.DeviceId, x.CpuPercent, x.RamPercent, x.DiskPercent, x.CollectedAt ?? x.CreatedAt, x.CollectedAt, x.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(snapshots);
        }).RequireAuthorization()
            .Produces<IReadOnlyList<TelemetrySnapshotDto>>(StatusCodes.Status200OK)
            .WithSummary("Read technical measurements in original collection-time order")
            .WithDescription("CreatedAt uses collection time when known. CollectedAt is null for legacy samples; ReceivedAt is server receipt time. Only the last 30 days are returned.");

        v1.MapGet("/dashboard", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var now = DateTimeOffset.UtcNow;
            var query = DeviceScope.ForActor(db.Devices.AsNoTracking(), actor);
            var total = await query.CountAsync(ct);
            var cutoff = now - Device.OnlineTimeout;
            var online = await query.CountAsync(x => !x.IsRevoked && x.LastSeenAt != null && x.LastSeenAt > cutoff, ct);
            var devices = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).ToListAsync(ct);
            var dto = devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now), x.AssignedUserId, x.IsRevoked)).ToList();
            return Results.Ok(new DashboardDto(total, online, total - online, await db.Alerts.CountAsync(x => x.OrganizationId == actor.OrganizationId && x.IsOpen, ct), dto));
        }).RequireAuthorization(AuthorizationPolicies.ViewDevices);
    }

    private static async Task<IResult> PublishDeviceUpdateAsync(DeviceDto device, IHubContext<UpdatesHub> hub, HttpContext http, CancellationToken cancellationToken)
    {
        var actor = http.User.ToActorContext()!.Value;
        await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("device-status", device, cancellationToken);
        return Results.Ok(device);
    }
}
