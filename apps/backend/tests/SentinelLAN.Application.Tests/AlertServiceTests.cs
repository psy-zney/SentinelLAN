using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class AlertServiceTests
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly Guid deviceId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private ActorContext Actor => new(userId, orgId, Roles.Admin);

    [Fact]
    public async Task TriggerAlertCreatesAlertAndLogsAudit()
    {
        var store = new FakeAlertStore();
        var service = new AlertService(store, TimeProvider.System);

        var alert = await service.TriggerAlertAsync(orgId, deviceId, "Warning", "CPU sustained over 90%", CancellationToken.None);

        Assert.NotNull(alert);
        Assert.Equal("Warning", alert.Severity);
        Assert.Equal("CPU sustained over 90%", alert.Message);
        Assert.True(alert.IsOpen);
        Assert.Null(alert.AcknowledgedAt);
        Assert.Null(alert.ResolvedAt);
        Assert.Single(store.Alerts);
        Assert.Contains(store.Audits, e => e.Action == "AlertTriggered:Warning");
    }

    [Fact]
    public async Task AcknowledgeAlertSetsAcknowledgedAt()
    {
        var store = new FakeAlertStore();
        var existing = new Alert { OrganizationId = orgId, DeviceId = deviceId, Severity = "Critical", Message = "Disk > 95%", IsOpen = true };
        store.Alerts.Add(existing);
        var service = new AlertService(store, TimeProvider.System);

        var result = await service.AcknowledgeAlertAsync(Actor, existing.Id, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(existing.AcknowledgedAt);
        Assert.Contains(store.Audits, e => e.Action == "AlertAcknowledged");
    }

    [Fact]
    public async Task ResolveAlertSetsResolvedAtAndUserAndLogsAudit()
    {
        var store = new FakeAlertStore();
        var existing = new Alert { OrganizationId = orgId, DeviceId = deviceId, Severity = "Critical", Message = "Disk > 95%", IsOpen = true };
        store.Alerts.Add(existing);
        var service = new AlertService(store, TimeProvider.System);

        var result = await service.ResolveAlertAsync(Actor, existing.Id, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(existing.ResolvedAt);
        Assert.Equal(userId, existing.ResolvedByUserId);
        Assert.False(existing.IsOpen);
        Assert.Contains(store.Audits, e => e.Action == "AlertResolved");
    }

    private sealed class FakeAlertStore : IAlertStore
    {
        public List<Alert> Alerts { get; } = [];
        public List<AuditLog> Audits { get; } = [];

        public Task<IReadOnlyList<AlertDto>> GetAlertsAsync(Guid organizationId, bool? onlyOpen, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AlertDto>>(Alerts
                .Where(a => a.OrganizationId == organizationId && (!onlyOpen.HasValue || (onlyOpen.Value ? a.IsOpen : !a.IsOpen)))
                .Select(a => new AlertDto(a.Id, a.DeviceId, null, a.Severity, a.Message, a.IsOpen, a.AcknowledgedAt, a.ResolvedAt, a.CreatedAt))
                .ToList());

        public Task<Alert?> FindAlertAsync(Guid organizationId, Guid alertId, CancellationToken cancellationToken) =>
            Task.FromResult(Alerts.SingleOrDefault(a => a.OrganizationId == organizationId && a.Id == alertId));

        public void AddAlert(Alert alert) => Alerts.Add(alert);
        public void AddAudit(AuditLog log) => Audits.Add(log);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
