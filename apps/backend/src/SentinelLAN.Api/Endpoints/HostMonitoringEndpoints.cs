using SentinelLAN.Application;

namespace SentinelLAN.Api.Endpoints;

public static class HostMonitoringEndpoints
{
    public static void MapHostMonitoringEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/host/status", async (HttpContext http, VpsHostMonitorService monitoring, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            try
            {
                return Results.Ok(await monitoring.GetStatusAsync(http.User.ToActorContext()!.Value, ct));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization(AuthorizationPolicies.Admin)
            .Produces<VpsHostStatusDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("VPS monitoring");
    }
}
