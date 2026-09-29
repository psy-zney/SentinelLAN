using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/commands", async (CreateCommandRequest request, HttpContext http, SentinelDbContext db, ICommandSigner signer, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            if (request.Type is "InstallApprovedApp" or "PauseAgent" or "UninstallAgent") return Results.BadRequest(new ProblemDetails { Title = "This command requires an approved self-service request and maintenance OTP when applicable", Status = 400 });
            if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.ValidForSeconds is < 30 or > 900) return Results.BadRequest(new ProblemDetails { Title = "Confirmation, a reason (up to 1000 characters) and a 30-900 second validity are required", Status = 400 });
            var device = await DeviceScope.ForActor(db.Devices, actor).SingleOrDefaultAsync(x => x.Id == request.DeviceId, ct);
            if (device is null) return Results.NotFound();
            if (device.IsRevoked) return Results.Conflict(new ProblemDetails { Title = "Device is revoked", Status = 409 });
            var command = new DeviceCommand
            {
                OrganizationId = device.OrganizationId,
                DeviceId = device.Id,
                IssuedByUserId = actor.UserId,
                Type = request.Type,
                Reason = request.Reason.Trim(),
                Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                Signature = "pending",
                Parameter = request.Parameter?.Trim(),
                IssuedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(request.ValidForSeconds),
                Status = DeviceCommandStatus.Pending
            };
            if (!command.CanDeliver(DateTimeOffset.UtcNow)) return Results.BadRequest(new ProblemDetails { Title = "Unsupported command type", Status = 400 });
            command.Signature = signer.Sign(command);
            db.Add(command);
            db.Add(new AuditLog { OrganizationId = device.OrganizationId, ActorId = actor.UserId, DeviceId = device.Id, Action = $"CommandCreated:{command.Type}", Reason = command.Reason, Outcome = "Pending" });
            await db.SaveChangesAsync(ct);
            await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
            return Results.Created($"/api/v1/commands/{command.Id}", command);
        }).RequireAuthorization(AuthorizationPolicies.ManageCommands);

        v1.MapGet("/commands", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var commands = await (
                from c in db.Commands.AsNoTracking()
                join d in db.Devices.AsNoTracking() on c.DeviceId equals d.Id
                where c.OrganizationId == actor.OrganizationId
                orderby c.CreatedAt descending
                select new
                {
                    Command = c,
                    DeviceName = d.Name,
                    Result = db.CommandResults.AsNoTracking().FirstOrDefault(r => r.CommandId == c.Id)
                })
                .Take(100)
                .ToListAsync(ct);

            return Results.Ok(commands.Select(x => new CommandDto(
                x.Command.Id,
                x.Command.DeviceId,
                x.DeviceName,
                x.Command.Type,
                x.Command.Reason,
                x.Command.Parameter,
                x.Command.Status.ToString(),
                x.Command.IssuedAt,
                x.Command.ExpiresAt,
                x.Result?.Succeeded,
                x.Result?.Message
            )));
        }).RequireAuthorization(AuthorizationPolicies.ManageCommands);

        v1.MapGet("/commands/{id:guid}", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var item = await (
                from c in db.Commands.AsNoTracking()
                join d in db.Devices.AsNoTracking() on c.DeviceId equals d.Id
                where c.OrganizationId == actor.OrganizationId && c.Id == id
                select new
                {
                    Command = c,
                    DeviceName = d.Name,
                    Result = db.CommandResults.AsNoTracking().FirstOrDefault(r => r.CommandId == c.Id)
                }).SingleOrDefaultAsync(ct);

            if (item is null) return Results.NotFound();

            return Results.Ok(new CommandDto(
                item.Command.Id,
                item.Command.DeviceId,
                item.DeviceName,
                item.Command.Type,
                item.Command.Reason,
                item.Command.Parameter,
                item.Command.Status.ToString(),
                item.Command.IssuedAt,
                item.Command.ExpiresAt,
                item.Result?.Succeeded,
                item.Result?.Message
            ));
        }).RequireAuthorization(AuthorizationPolicies.ManageCommands);
    }
}
