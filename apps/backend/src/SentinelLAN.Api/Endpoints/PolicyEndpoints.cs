using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class PolicyEndpoints
{
    public static void MapPolicyEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/policies", async (HttpContext http, PolicyService policyService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            return Results.Ok(await policyService.GetPoliciesAsync(actor, ct));
        }).RequireAuthorization(AuthorizationPolicies.ViewPolicies);

        v1.MapPost("/policies", async (CreatePolicyRequest request, HttpContext http, PolicyService policyService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var policy = await policyService.CreatePolicyAsync(actor, request, ct);
            return policy is not null ? Results.Created($"/api/v1/policies/{policy.Id}", policy) : Results.BadRequest(new ProblemDetails { Title = "Invalid policy details or name already in use", Status = 400 });
        }).RequireAuthorization(AuthorizationPolicies.ManagePolicies);

        v1.MapPut("/policies/{id:guid}", async (Guid id, UpdatePolicyRequest request, HttpContext http, PolicyService policyService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var policy = await policyService.UpdatePolicyAsync(actor, id, request, ct);
            return policy is not null ? Results.Ok(policy) : Results.BadRequest(new ProblemDetails { Title = "Invalid policy details or policy not found", Status = 400 });
        }).RequireAuthorization(AuthorizationPolicies.ManagePolicies);

        v1.MapPost("/policies/{id:guid}/assign", async (Guid id, AssignPolicyRequest request, HttpContext http, PolicyService policyService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            if (id != request.PolicyId) return Results.BadRequest(new ProblemDetails { Title = "Route ID must match payload PolicyId", Status = 400 });
            var succeeded = await policyService.AssignPolicyAsync(actor, request, ct);
            if (!succeeded) return Results.BadRequest(new ProblemDetails { Title = "Unable to assign policy to specified device", Status = 400 });
            await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("policy-assigned", new { policyId = id, deviceId = request.DeviceId }, ct);
            return Results.Ok(new { assigned = true });
        }).RequireAuthorization(AuthorizationPolicies.ManagePolicies);
    }
}
