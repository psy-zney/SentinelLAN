using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class PolicyServiceTests
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private ActorContext Actor => new(userId, orgId, Roles.Admin);

    [Fact]
    public async Task CreatePolicySucceedsAndLogsAudit()
    {
        var store = new FakePolicyStore();
        var service = new PolicyService(store);

        var request = new CreatePolicyRequest("Default Policy", 15, "ReadOnly");
        var result = await service.CreatePolicyAsync(Actor, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Default Policy", result.Name);
        Assert.Equal(15, result.IdleTimeoutMinutes);
        Assert.Equal("ReadOnly", result.UsbMode);
        Assert.Single(store.Policies);
        Assert.Contains(store.Audits, e => e.Action == "PolicyCreated");
    }

    [Fact]
    public async Task CreatePolicyDuplicateNameReturnsNull()
    {
        var store = new FakePolicyStore();
        store.Policies.Add(new Policy { OrganizationId = orgId, Name = "Existing", IdleTimeoutMinutes = 15, UsbMode = "Blocked" });
        var service = new PolicyService(store);

        var result = await service.CreatePolicyAsync(Actor, new CreatePolicyRequest("Existing", 30, "Blocked"), CancellationToken.None);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AllowAll")]
    public async Task CreatePolicyRejectsUnknownUsbModes(string usbMode)
    {
        var store = new FakePolicyStore();
        var service = new PolicyService(store);

        var result = await service.CreatePolicyAsync(Actor, new CreatePolicyRequest("USB policy", 15, usbMode), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePolicyUpdatesPropertiesAndLogsAudit()
    {
        var policy = new Policy { OrganizationId = orgId, Name = "Workstation", IdleTimeoutMinutes = 15, UsbMode = "Blocked" };
        var store = new FakePolicyStore();
        store.Policies.Add(policy);
        var service = new PolicyService(store);

        var updateReq = new UpdatePolicyRequest("Workstation Updated", 30, "FullAccess");
        var updated = await service.UpdatePolicyAsync(Actor, policy.Id, updateReq, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Workstation Updated", updated.Name);
        Assert.Equal(30, updated.IdleTimeoutMinutes);
        Assert.Equal("FullAccess", updated.UsbMode);
        Assert.Contains(store.Audits, e => e.Action == "PolicyUpdated");
    }

    [Fact]
    public async Task AssignPolicyValidDeviceAndPolicyAssignsAndLogsAudit()
    {
        var deviceId = Guid.NewGuid();
        var policy = new Policy { OrganizationId = orgId, Name = "Server Policy", IdleTimeoutMinutes = 60, UsbMode = "Blocked" };
        var store = new FakePolicyStore();
        store.Policies.Add(policy);
        store.ValidDeviceIds.Add(deviceId);
        var service = new PolicyService(store);

        var success = await service.AssignPolicyAsync(Actor, new AssignPolicyRequest(policy.Id, deviceId), CancellationToken.None);

        Assert.True(success);
        Assert.Single(store.Assignments);
        Assert.Equal(policy.Id, store.Assignments[0].PolicyId);
        Assert.Equal(deviceId, store.Assignments[0].DeviceId);
        Assert.Contains(store.Audits, e => e.Action == "PolicyAssigned");
    }

    [Fact]
    public async Task AssignPolicyDeviceNotFoundReturnsFalse()
    {
        var policy = new Policy { OrganizationId = orgId, Name = "Server Policy", IdleTimeoutMinutes = 60, UsbMode = "Blocked" };
        var store = new FakePolicyStore();
        store.Policies.Add(policy);
        var service = new PolicyService(store);

        var success = await service.AssignPolicyAsync(Actor, new AssignPolicyRequest(policy.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(success);
        Assert.Empty(store.Assignments);
    }

    [Fact]
    public async Task AssignPolicyNotFoundReturnsFalse()
    {
        var deviceId = Guid.NewGuid();
        var store = new FakePolicyStore();
        store.ValidDeviceIds.Add(deviceId);
        var service = new PolicyService(store);

        var success = await service.AssignPolicyAsync(Actor, new AssignPolicyRequest(Guid.NewGuid(), deviceId), CancellationToken.None);

        Assert.False(success);
        Assert.Empty(store.Assignments);
    }

    private sealed class FakePolicyStore : IPolicyStore
    {
        public List<Policy> Policies { get; } = [];
        public List<PolicyAssignment> Assignments { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public HashSet<Guid> ValidDeviceIds { get; } = [];

        public Task<IReadOnlyList<PolicyDto>> GetPoliciesAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PolicyDto>>(Policies
                .Where(p => p.OrganizationId == organizationId)
                .Select(p => new PolicyDto(p.Id, p.Name, p.IdleTimeoutMinutes, p.UsbMode, Assignments.Count(a => a.PolicyId == p.Id), p.CreatedAt))
                .ToList());

        public Task<Policy?> FindPolicyAsync(Guid organizationId, Guid policyId, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.SingleOrDefault(p => p.OrganizationId == organizationId && p.Id == policyId));

        public Task<bool> ExistsNameAsync(Guid organizationId, string name, Guid? excludePolicyId, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.Any(p => p.OrganizationId == organizationId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && (!excludePolicyId.HasValue || p.Id != excludePolicyId.Value)));

        public Task<bool> DeviceExistsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(ValidDeviceIds.Contains(deviceId));

        public Task<PolicyAssignment?> FindAssignmentAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(Assignments.SingleOrDefault(a => a.OrganizationId == organizationId && a.DeviceId == deviceId));

        public void AddPolicy(Policy policy) => Policies.Add(policy);
        public void AddAssignment(PolicyAssignment assignment) => Assignments.Add(assignment);
        public void RemoveAssignment(PolicyAssignment assignment) => Assignments.Remove(assignment);
        public void AddAudit(AuditLog log) => Audits.Add(log);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
