using SentinelLAN.Application;

namespace SentinelLAN.Api.Endpoints;

public static class OrganizationSettingsEndpoints
{
    public static void MapOrganizationSettingsEndpoints(this RouteGroupBuilder v1)
    {
        var settings = v1.MapGroup("/organization/monitoring-settings").RequireAuthorization(AuthorizationPolicies.Admin).WithTags("Organization settings");
        settings.MapGet("", async (HttpContext http, MonitoringSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(http.User.ToActorContext()!.Value, ct))).Produces<MonitoringSettingsDto>();
        settings.MapPut("", async (UpdateMonitoringSettingsRequest input, HttpContext http, MonitoringSettingsService service,
            IMonitoringChangeSignal signal, CancellationToken ct) =>
        {
            try
            {
                var actor = http.User.ToActorContext()!.Value;
                var result = await service.UpdateAsync(actor, input, ct);
                signal.RequestCheck(actor.OrganizationId);
                return Results.Ok(result);
            }
            catch (OrganizationConfigurationException error) { return Results.Problem(error.Message, statusCode: error.StatusCode); }
        }).RequireRateLimiting("sensitive").Produces<MonitoringSettingsDto>()
            .WithDescription("Updates this organization's monitoring thresholds. Requires confirmation, reason, a single-use nonce, expiry within five minutes and the current revision; queues an immediate health check.");
    }
}
