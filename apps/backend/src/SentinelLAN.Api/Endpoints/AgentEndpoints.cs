using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/agent/enroll", async (EnrollRequest request, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            if (!DeviceRequestValidation.IsValid(request)) return Results.BadRequest(new ProblemDetails { Title = "Valid token, device name, OS and Agent version are required", Status = 400 });
            var hash = SecretHash.Create(request.Token);
            var token = await db.EnrollmentTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            var now = DateTimeOffset.UtcNow;
            if (token is null)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
            }
            if (!await db.Organizations.AnyAsync(o => o.Id == token.OrganizationId && !o.IsSuspended, ct))
                return Results.BadRequest(new ProblemDetails { Title = "Organization unavailable", Status = 400 });
            if (!token.TryUse(now))
            {
                var failureReason = token.UsedAt is not null ? "Token already used" : "Token expired";
                db.Add(new AuditLog
                {
                    OrganizationId = token.OrganizationId,
                    ActorId = token.Id,
                    DeviceId = null,
                    Action = "AgentEnrollmentFailed",
                    Reason = failureReason,
                    Outcome = "Failed"
                });
                await db.SaveChangesAsync(ct);
                return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
            }

            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var device = new Device { OrganizationId = token.OrganizationId, Name = request.DeviceName, OsVersion = request.OsVersion, AgentVersion = request.AgentVersion };
            db.Add(device);
            db.Add(new DeviceCredential { OrganizationId = token.OrganizationId, DeviceId = device.Id, SecretHash = SecretHash.Create(secret) });
            db.Add(new AuditLog
            {
                OrganizationId = token.OrganizationId,
                ActorId = device.Id,
                DeviceId = device.Id,
                Action = "AgentEnrolled",
                Reason = $"Device '{request.DeviceName}' successfully enrolled.",
                Outcome = "Success"
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                db.Add(new AuditLog { OrganizationId = token.OrganizationId, ActorId = token.Id, Action = "AgentEnrollmentFailed", Reason = "Token already used", Outcome = "Failed" });
                await db.SaveChangesAsync(ct);
                return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
            }
            await hub.Clients.Group(TenantGroup.Name(token.OrganizationId)).SendAsync("device-status", new { device.Id, online = false, device.LastSeenAt }, ct);
            return Results.Ok(new EnrollResponse(device.Id, secret));
        }).RequireRateLimiting("sensitive");

        v1.MapPost("/agent/heartbeat", async (HeartbeatRequest request, HttpContext http, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var agent = http.User.ToAgentContext()!.Value;
            if (!DeviceRequestValidation.IsValid(request)) return Results.BadRequest(new ProblemDetails { Title = "Valid idempotency key, 0-100 telemetry percentages, OS and Agent version are required", Status = 400 });
            if (await db.Heartbeats.AnyAsync(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.IdempotencyKey == request.IdempotencyKey, ct)) return Results.Ok(new { duplicate = true });
            var device = await db.Devices.SingleAsync(x => x.Id == agent.DeviceId && x.OrganizationId == agent.OrganizationId, ct);
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.OsVersion = request.OsVersion;
            device.AgentVersion = request.AgentVersion;
            device.MaintenanceUntil = request.MaintenanceUntil?.ToUniversalTime();
            device.MaintenanceAction = request.MaintenanceAction;
            db.Add(new DeviceHeartbeat { OrganizationId = device.OrganizationId, DeviceId = device.Id, IdempotencyKey = request.IdempotencyKey, RecordedAt = DateTimeOffset.UtcNow });
            db.Add(new TelemetrySnapshot { OrganizationId = device.OrganizationId, DeviceId = device.Id, CpuPercent = request.CpuPercent, RamPercent = request.RamPercent, DiskPercent = request.DiskPercent });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (await db.Heartbeats.AnyAsync(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.IdempotencyKey == request.IdempotencyKey, ct))
                    return Results.Ok(new { duplicate = true });
                throw;
            }
            await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("device-status", new { device.Id, online = true, device.LastSeenAt }, ct);
            return Results.Accepted();
        }).RequireAuthorization(AuthorizationPolicies.Agent).RequireRateLimiting("sensitive");

        v1.MapGet("/agent/policy", async (HttpContext http, PolicyService policies, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var policy = await policies.GetAssignedPolicyAsync(http.User.ToAgentContext()!.Value, ct);
            return policy is null ? Results.NoContent() : Results.Ok(policy);
        }).RequireAuthorization(AuthorizationPolicies.Agent)
            .WithSummary("Read the authenticated device's assigned Windows policy")
            .Produces<AgentPolicyDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        v1.MapPost("/agent/commands/poll", async (HttpContext http, SentinelDbContext db, ICommandSigner signer, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var agent = http.User.ToAgentContext()!.Value;
            var now = DateTimeOffset.UtcNow;
            var expired = await db.Commands.Where(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && (x.Status == DeviceCommandStatus.Pending || x.Status == DeviceCommandStatus.Delivered) && x.ExpiresAt <= now).ToListAsync(ct);
            foreach (var item in expired)
            {
                item.Status = DeviceCommandStatus.Expired;
                item.DeliveryLeaseExpiresAt = null;
            }
            var command = await db.Commands.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x =>
                x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.ExpiresAt > now &&
                (x.Status == DeviceCommandStatus.Pending ||
                 (x.Status == DeviceCommandStatus.Delivered && x.DeliveryLeaseExpiresAt <= now)), ct);
            if (command is null)
            {
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException) { return Results.NoContent(); }
                return Results.NoContent();
            }
            if (!signer.Verify(command)) return Results.Problem("Command signature validation failed", statusCode: 409);
            if (!command.TryLeaseForDelivery(now, TimeSpan.FromSeconds(30))) return Results.NoContent();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.NoContent(); }
            return Results.Ok(command);
        }).RequireAuthorization(AuthorizationPolicies.Agent).RequireRateLimiting("sensitive");

        v1.MapPost("/agent/commands/{id:guid}/result", async (Guid id, CommandResultRequest request, HttpContext http, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var agent = http.User.ToAgentContext()!.Value;
            var command = await db.Commands.SingleOrDefaultAsync(x => x.Id == id && x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId, ct);
            if (command is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000) return Results.BadRequest();
            if (await db.CommandResults.AnyAsync(x => x.CommandId == id && x.OrganizationId == agent.OrganizationId, ct)) return Results.Ok(new { duplicate = true });
            if (command.Status != DeviceCommandStatus.Delivered || command.ExpiresAt <= DateTimeOffset.UtcNow)
                return Results.Conflict(new ProblemDetails { Title = "Only a delivered, unexpired command can receive a result", Status = 409 });
            command.Status = request.Succeeded ? DeviceCommandStatus.Succeeded : DeviceCommandStatus.Failed;
            command.DeliveryLeaseExpiresAt = null;
            db.Add(new CommandResult { OrganizationId = command.OrganizationId, DeviceId = command.DeviceId, CommandId = id, Succeeded = request.Succeeded, Message = request.Message });
            db.Add(new AuditLog { OrganizationId = command.OrganizationId, ActorId = agent.DeviceId, DeviceId = command.DeviceId, Action = $"CommandCompleted:{command.Id:N}:{command.Type}", Reason = command.Reason, Outcome = command.Status.ToString() });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (await db.CommandResults.AnyAsync(x => x.CommandId == id && x.OrganizationId == agent.OrganizationId, ct))
                    return Results.Ok(new { duplicate = true });
                throw;
            }
            await hub.Clients.Group(TenantGroup.Name(command.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
            return Results.Accepted();
        }).RequireAuthorization(AuthorizationPolicies.Agent);
    }
}
