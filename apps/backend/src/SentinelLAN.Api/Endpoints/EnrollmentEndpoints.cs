using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/enrollment-tokens", async (EnrollmentTokenRequest request, HttpContext http, SentinelDbContext db, EnrollmentTokenService tokens, CancellationToken ct) =>
        {
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var result = await tokens.CreateAsync(actor, request, ct);
            if (result.Status == ManagementResultStatus.Forbidden) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (result.Status != ManagementResultStatus.Succeeded) return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token details", Status = 400 });
            http.Response.Headers.CacheControl = "no-store";
            return Results.Created("/api/v1/enrollment-tokens", result.Token);
        })
            .WithName("CreateEnrollmentToken")
            .Produces<EnrollmentTokenResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .RequireRateLimiting("sensitive");
    }
}
