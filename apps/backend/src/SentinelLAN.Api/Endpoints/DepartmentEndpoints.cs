using SentinelLAN.Application;

namespace SentinelLAN.Api.Endpoints;

public static class DepartmentEndpoints
{
    public static void MapDepartmentEndpoints(this RouteGroupBuilder v1)
    {
        var group = v1.MapGroup("/departments").RequireAuthorization(AuthorizationPolicies.Admin).WithTags("Departments");
        group.MapGet("", async (HttpContext http, DepartmentService service, CancellationToken ct) => Results.Ok(await service.DirectoryAsync(http.User.ToActorContext()!.Value, ct))).Produces<DepartmentDirectoryDto>();
        group.MapPost("", (SaveDepartmentRequest request, HttpContext http, DepartmentService service, CancellationToken ct) => Handle(() => service.SaveAsync(http.User.ToActorContext()!.Value, null, request, ct))).RequireRateLimiting("sensitive");
        group.MapPut("/{id:guid}", (Guid id, SaveDepartmentRequest request, HttpContext http, DepartmentService service, CancellationToken ct) => Handle(() => service.SaveAsync(http.User.ToActorContext()!.Value, id, request, ct))).RequireRateLimiting("sensitive");
        group.MapPut("/users/{id:guid}", (Guid id, DepartmentMembershipRequest request, HttpContext http, DepartmentService service, CancellationToken ct) => Handle(() => service.AssignAsync(http.User.ToActorContext()!.Value, "users", id, request, ct))).RequireRateLimiting("sensitive");
        group.MapPut("/devices/{id:guid}", (Guid id, DepartmentMembershipRequest request, HttpContext http, DepartmentService service, CancellationToken ct) => Handle(() => service.AssignAsync(http.User.ToActorContext()!.Value, "devices", id, request, ct))).RequireRateLimiting("sensitive");
        v1.MapPost("/commands/bulk", async (BulkCommandRequest request, HttpContext http, DepartmentService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.BulkAsync(http.User.ToActorContext()!.Value, request, ct)); }
            catch (OrganizationConfigurationException e) { return Results.Problem(e.Message, statusCode: e.StatusCode); }
        }).RequireAuthorization(AuthorizationPolicies.Admin).RequireRateLimiting("sensitive").Produces<IReadOnlyList<BulkCommandResult>>();
    }
    private static async Task<IResult> Handle(Func<Task> action)
    {
        try { await action(); return Results.NoContent(); }
        catch (OrganizationConfigurationException e) { return Results.Problem(e.Message, statusCode: e.StatusCode); }
    }
}
