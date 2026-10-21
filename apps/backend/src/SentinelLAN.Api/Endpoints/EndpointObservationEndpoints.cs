using SentinelLAN.Application;

namespace SentinelLAN.Api.Endpoints;

public static class EndpointObservationEndpoints
{
    public static void MapEndpointObservationEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/devices/{id:guid}/observations", async (Guid id, HttpContext http, EndpointObservationService service, CancellationToken ct) =>
            await service.GetAsync(http.User.ToActorContext()!.Value, id, ct) is { } value ? Results.Ok(value) : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.Admin).Produces<EndpointObservationDto>().WithTags("Devices");
        v1.MapGet("/my-device/observations", async (HttpContext http, EndpointObservationService service, CancellationToken ct) =>
            await service.GetAsync(http.User.ToActorContext()!.Value, null, ct) is { } value ? Results.Ok(value) : Results.NotFound())
            .RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).Produces<EndpointObservationDto>().WithTags("My device");
    }
}
