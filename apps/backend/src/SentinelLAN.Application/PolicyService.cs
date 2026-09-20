using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IPolicyStore
{
    Task<IReadOnlyList<PolicyDto>> GetPoliciesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Policy?> FindPolicyAsync(Guid organizationId, Guid policyId, CancellationToken cancellationToken);
    Task<bool> ExistsNameAsync(Guid organizationId, string name, Guid? excludePolicyId, CancellationToken cancellationToken);
    Task<bool> DeviceExistsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<PolicyAssignment?> FindAssignmentAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    void AddPolicy(Policy policy);
    void AddAssignment(PolicyAssignment assignment);
    void RemoveAssignment(PolicyAssignment assignment);
    void AddAudit(AuditLog log);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class PolicyService(IPolicyStore store)
{
    private static readonly HashSet<string> AllowedUsbModes = ["Blocked", "ReadOnly", "FullAccess"];

    public Task<IReadOnlyList<PolicyDto>> GetPoliciesAsync(ActorContext actor, CancellationToken cancellationToken) =>
        store.GetPoliciesAsync(actor.OrganizationId, cancellationToken);

    public async Task<PolicyDto?> CreatePolicyAsync(ActorContext actor, CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 100) return null;
        if (request.IdleTimeoutMinutes is < 1 or > 1440) return null;
        if (string.IsNullOrWhiteSpace(request.UsbMode) || !AllowedUsbModes.Contains(request.UsbMode.Trim(), StringComparer.Ordinal)) return null;

        var name = request.Name.Trim();
        if (await store.ExistsNameAsync(actor.OrganizationId, name, null, cancellationToken)) return null;

        var policy = new Policy
        {
            OrganizationId = actor.OrganizationId,
            Name = name,
            IdleTimeoutMinutes = request.IdleTimeoutMinutes,
            UsbMode = request.UsbMode.Trim()
        };

        store.AddPolicy(policy);
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "PolicyCreated",
            Reason = $"Policy '{policy.Name}' created (IdleTimeout: {policy.IdleTimeoutMinutes}m, UsbMode: {policy.UsbMode})",
            Outcome = "Success"
        });

        await store.SaveChangesAsync(cancellationToken);
        return new PolicyDto(policy.Id, policy.Name, policy.IdleTimeoutMinutes, policy.UsbMode, 0, policy.CreatedAt);
    }

    public async Task<PolicyDto?> UpdatePolicyAsync(ActorContext actor, Guid id, UpdatePolicyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 100) return null;
        if (request.IdleTimeoutMinutes is < 1 or > 1440) return null;
        if (string.IsNullOrWhiteSpace(request.UsbMode) || !AllowedUsbModes.Contains(request.UsbMode.Trim(), StringComparer.Ordinal)) return null;

        var policy = await store.FindPolicyAsync(actor.OrganizationId, id, cancellationToken);
        if (policy is null) return null;

        var name = request.Name.Trim();
        if (await store.ExistsNameAsync(actor.OrganizationId, name, id, cancellationToken)) return null;

        policy.Name = name;
        policy.IdleTimeoutMinutes = request.IdleTimeoutMinutes;
        policy.UsbMode = request.UsbMode.Trim();
        policy.UpdatedAt = DateTimeOffset.UtcNow;

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = null,
            Action = "PolicyUpdated",
            Reason = $"Policy '{policy.Name}' updated",
            Outcome = "Success"
        });

        await store.SaveChangesAsync(cancellationToken);
        var currentPolicies = await store.GetPoliciesAsync(actor.OrganizationId, cancellationToken);
        return currentPolicies.FirstOrDefault(p => p.Id == id);
    }

    public async Task<bool> AssignPolicyAsync(ActorContext actor, AssignPolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = await store.FindPolicyAsync(actor.OrganizationId, request.PolicyId, cancellationToken);
        if (policy is null) return false;

        if (!await store.DeviceExistsAsync(actor.OrganizationId, request.DeviceId, cancellationToken)) return false;

        var existing = await store.FindAssignmentAsync(actor.OrganizationId, request.DeviceId, cancellationToken);
        if (existing is not null)
        {
            store.RemoveAssignment(existing);
        }

        var assignment = new PolicyAssignment
        {
            OrganizationId = actor.OrganizationId,
            PolicyId = policy.Id,
            DeviceId = request.DeviceId
        };
        store.AddAssignment(assignment);

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = request.DeviceId,
            Action = "PolicyAssigned",
            Reason = $"Policy '{policy.Name}' assigned to device {request.DeviceId}",
            Outcome = "Success"
        });

        await store.SaveChangesAsync(cancellationToken);
        return true;
    }
}
