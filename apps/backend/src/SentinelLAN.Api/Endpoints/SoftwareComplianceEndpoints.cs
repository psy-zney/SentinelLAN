using SentinelLAN.Application;

namespace SentinelLAN.Api.Endpoints;

public static class SoftwareComplianceEndpoints
{
    public static void MapSoftwareComplianceEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/agent/software-rules", async (HttpContext http, SoftwareComplianceService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.DetectionRulesAsync(http.User.ToAgentContext()!.Value.OrganizationId, ct)); }
            catch (SoftwareComplianceException error) { return Results.Problem(error.Message, statusCode: error.StatusCode); }
        }).RequireAuthorization(AuthorizationPolicies.Agent).Produces<IReadOnlyList<SoftwareDetectionRuleDto>>().WithTags("Agent software");
        v1.MapGet("/devices/{id:guid}/software", async (Guid id, HttpContext http, SoftwareComplianceService service, CancellationToken ct) =>
            await service.StatusAsync(http.User.ToActorContext()!.Value, id, ct) is { } value ? Results.Ok(value) : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.Admin).Produces<IReadOnlyList<SoftwareComplianceDto>>();
        v1.MapGet("/my-device/software", async (HttpContext http, SoftwareComplianceService service, CancellationToken ct) =>
            await service.StatusAsync(http.User.ToActorContext()!.Value, null, ct) is { } value ? Results.Ok(value) : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).Produces<IReadOnlyList<SoftwareComplianceDto>>();
        var rules = v1.MapGroup("/software/prohibited-rules").RequireAuthorization(AuthorizationPolicies.Admin).WithTags("Software policy");
        rules.MapGet("", async (HttpContext http, SoftwareComplianceService service, CancellationToken ct) =>
            Results.Ok(await service.RulesAsync(http.User.ToActorContext()!.Value, ct))).Produces<IReadOnlyList<ProhibitedSoftwareRuleDto>>();
        rules.MapPost("", (SaveProhibitedSoftwareRuleRequest input, HttpContext http, SoftwareComplianceService service, CancellationToken ct) => SaveAsync(null, input, http, service, ct)).RequireRateLimiting("sensitive").Produces<ProhibitedSoftwareRuleDto>();
        rules.MapPut("/{id:guid}", (Guid id, SaveProhibitedSoftwareRuleRequest input, HttpContext http, SoftwareComplianceService service, CancellationToken ct) => SaveAsync(id, input, http, service, ct)).RequireRateLimiting("sensitive").Produces<ProhibitedSoftwareRuleDto>();
    }

    private static async Task<IResult> SaveAsync(Guid? id, SaveProhibitedSoftwareRuleRequest input, HttpContext http, SoftwareComplianceService service, CancellationToken ct)
    {
        try { return Results.Ok(await service.SaveRuleAsync(http.User.ToActorContext()!.Value, id, input, ct)); }
        catch (SoftwareComplianceException error) { return Results.Problem(error.Message, statusCode: error.StatusCode); }
    }
}
