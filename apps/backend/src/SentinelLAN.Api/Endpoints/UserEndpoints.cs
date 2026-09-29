using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/users", async (CreateUserRequest request, HttpContext http, SentinelDbContext db, UserManagementService users, IPasswordHasher passwordHasher, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var result = await users.CreateAsync(actor, request, passwordHasher, ct);
            return result.Status switch
            {
                ManagementResultStatus.Succeeded => Results.Created($"/api/v1/users/{result.User!.Id}", new CreateUserResponse(result.User, result.ActivationToken, result.ActivationUrl, result.ExpiresAt)),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "A user with that email already exists", Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid user details", Status = 400 })
            };
        })
            .WithName("CreateUser")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapPut("/users/{id:guid}/status", async (Guid id, SetUserStatusRequest request, HttpContext http, SentinelDbContext db, UserManagementService users, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var (status, user) = await users.SetStatusAsync(actor, id, request, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(user),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ManagementResultStatus.NotFound => Results.NotFound(),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Account state cannot be changed", Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Status, reason and confirmation are required", Status = 400 })
            };
        }).WithName("SetUserStatus")
          .Produces<UserSummaryDto>(StatusCodes.Status200OK)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status403Forbidden)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict)
          .RequireAuthorization(AuthorizationPolicies.Admin)
          .RequireRateLimiting("sensitive");

        v1.MapPost("/users/{id:guid}/activation-token", async (Guid id, ReissueActivationTokenRequest req, HttpContext http, SentinelDbContext db, UserManagementService users, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            var actor = http.User.ToActorContext()!.Value;
            var (status, response, message) = await users.ReissueActivationTokenAsync(actor, id, req, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.Ok(response),
                ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "User not found", Detail = message, Status = 404 }),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Conflict", Detail = message, Status = 409 }),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
            };
        }).RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapDelete("/users/{id:guid}/activation-token", async (Guid id, [FromBody] RevokeActivationTokenRequest? req, HttpContext http, SentinelDbContext db, UserManagementService users, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var request = req ?? new RevokeActivationTokenRequest("Revoked by administrator", true);
            var (status, message) = await users.RevokeActivationTokenAsync(actor, id, request, ct);
            return status switch
            {
                ManagementResultStatus.Succeeded => Results.NoContent(),
                ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "User not found", Detail = message, Status = 404 }),
                ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
            };
        }).RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapGet("/users", async (HttpContext http, UserManagementService users, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var list = await users.GetUsersAsync(actor, ct);
            return Results.Ok(list);
        }).RequireAuthorization(AuthorizationPolicies.Admin);

        v1.MapGet("/organizations", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var org = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == actor.OrganizationId, ct);
            return org is null ? Results.NotFound() : Results.Ok(new OrganizationSummaryDto(org.Id, org.Code, org.Name, org.CreatedAt));
        }).RequireAuthorization(AuthorizationPolicies.Admin);
    }
}
