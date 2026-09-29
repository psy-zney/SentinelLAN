using Microsoft.EntityFrameworkCore;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { product = "SentinelLAN", version = "0.1.0", openApi = "/openapi/v1.json" }));
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", async (SentinelDbContext db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.Problem("Database unavailable", statusCode: 503));
    }
}
