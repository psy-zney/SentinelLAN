using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class PolicyStore(SentinelDbContext db) : IPolicyStore
{
    public async Task<IReadOnlyList<PolicyDto>> GetPoliciesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var policies = await db.Policies
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var assignmentCounts = await db.PolicyAssignments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .GroupBy(a => a.PolicyId)
            .Select(g => new { PolicyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PolicyId, x => x.Count, cancellationToken);

        return policies.Select(p => new PolicyDto(
            p.Id,
            p.Name,
            p.IdleTimeoutMinutes,
            p.UsbMode,
            assignmentCounts.GetValueOrDefault(p.Id, 0),
            p.CreatedAt
        )).ToList();
    }

    public Task<Policy?> FindPolicyAsync(Guid organizationId, Guid policyId, CancellationToken cancellationToken) =>
        db.Policies.SingleOrDefaultAsync(p => p.OrganizationId == organizationId && p.Id == policyId, cancellationToken);

    public Task<bool> ExistsNameAsync(Guid organizationId, string name, Guid? excludePolicyId, CancellationToken cancellationToken) =>
        db.Policies.AnyAsync(p => p.OrganizationId == organizationId && p.Name.ToUpper() == name.ToUpper() && (!excludePolicyId.HasValue || p.Id != excludePolicyId.Value), cancellationToken);

    public Task<bool> DeviceExistsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.Devices.AnyAsync(d => d.OrganizationId == organizationId && d.Id == deviceId && !d.IsRevoked, cancellationToken);

    public Task<PolicyAssignment?> FindAssignmentAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.PolicyAssignments.SingleOrDefaultAsync(a => a.OrganizationId == organizationId && a.DeviceId == deviceId, cancellationToken);

    public void AddPolicy(Policy policy) => db.Policies.Add(policy);

    public void AddAssignment(PolicyAssignment assignment) => db.PolicyAssignments.Add(assignment);

    public void RemoveAssignment(PolicyAssignment assignment) => db.PolicyAssignments.Remove(assignment);

    public void AddAudit(AuditLog log) => db.AuditLogs.Add(log);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
