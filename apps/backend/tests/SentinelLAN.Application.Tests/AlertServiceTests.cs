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
        var devices = new FakeAlertDeviceLookup();
        var service = new AlertService(store, devices, TimeProvider.System);

        var alert = await service.TriggerAlertAsync(Actor, deviceId, " Warning ", "  CPU sustained over 90%  ", CancellationToken.None);

        Assert.NotNull(alert);
        Assert.Equal("Warning", alert.Severity);
        Assert.Equal("CPU sustained over 90%", alert.Message);
        Assert.True(alert.IsOpen);
        Assert.Null(alert.AcknowledgedAt);
        Assert.Null(alert.ResolvedAt);
        Assert.Single(store.Alerts);
        var audit = Assert.Single(store.Audits, e => e.Action == "AlertTriggered:Warning");
        Assert.Equal(userId, audit.ActorId);
    }

    [Fact]
    public async Task AcknowledgeAlertSetsAcknowledgedAt()
    {
        var store = new FakeAlertStore();
        var existing = new Alert { OrganizationId = orgId, DeviceId = deviceId, Severity = "Critical", Message = "Disk > 95%", IsOpen = true };
        store.Alerts.Add(existing);
        var service = new AlertService(store, new FakeAlertDeviceLookup(), TimeProvider.System);

        var result = await service.AcknowledgeAlertAsync(Actor, existing.Id, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(existing.AcknowledgedAt);
        Assert.Contains(store.Audits, e => e.Action == "AlertAcknowledged");

        Assert.True(await service.AcknowledgeAlertAsync(Actor, existing.Id, CancellationToken.None));
        Assert.Single(store.Audits, e => e.Action == "AlertAcknowledged");
    }

    [Fact]
    public async Task ResolveAlertSetsResolvedAtAndUserAndLogsAudit()
    {
        var store = new FakeAlertStore();
        var existing = new Alert { OrganizationId = orgId, DeviceId = deviceId, Severity = "Critical", Message = "Disk > 95%", IsOpen = true };
        store.Alerts.Add(existing);
        var service = new AlertService(store, new FakeAlertDeviceLookup(), TimeProvider.System);

        var result = await service.ResolveAlertAsync(Actor, existing.Id, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(existing.ResolvedAt);
        Assert.Equal(userId, existing.ResolvedByUserId);
        Assert.False(existing.IsOpen);
        Assert.Contains(store.Audits, e => e.Action == "AlertResolved");

        Assert.True(await service.ResolveAlertAsync(Actor, existing.Id, CancellationToken.None));
        Assert.Single(store.Audits, e => e.Action == "AlertResolved");
    }

    [Fact]
    public async Task TriggerRejectsInvalidSeverityMessageRoleAndForeignDevice()
    {
        var store = new FakeAlertStore();
        var devices = new FakeAlertDeviceLookup { Exists = false };
        var service = new AlertService(store, devices, TimeProvider.System);

        Assert.Null(await service.TriggerAlertAsync(Actor, deviceId, "High", "Valid message", CancellationToken.None));
        Assert.Null(await service.TriggerAlertAsync(Actor, deviceId, "Warning", "  ", CancellationToken.None));
        Assert.Null(await service.TriggerAlertAsync(Actor, deviceId, "Warning", new string('x', 1001), CancellationToken.None));
        Assert.Null(await service.TriggerAlertAsync(new ActorContext(userId, orgId, Roles.Employee), deviceId, "Warning", "Valid message", CancellationToken.None));
        Assert.Null(await service.TriggerAlertAsync(Actor, deviceId, "Warning", "Valid message", CancellationToken.None));
        Assert.Empty(store.Alerts);
        Assert.Empty(store.Audits);
    }

    [Fact]
    public async Task AlertReadsAndMutationsRequireAlertPermissions()
    {
        var store = new FakeAlertStore();
        var existing = new Alert { OrganizationId = orgId, DeviceId = deviceId, Severity = "Info", Message = "Informational", IsOpen = true };
        store.Alerts.Add(existing);
        var service = new AlertService(store, new FakeAlertDeviceLookup(), TimeProvider.System);
        var employee = new ActorContext(userId, orgId, Roles.Employee);

        Assert.Empty(await service.GetAlertsAsync(employee, null, CancellationToken.None));
        Assert.False(await service.AcknowledgeAlertAsync(employee, existing.Id, CancellationToken.None));
        Assert.False(await service.ResolveAlertAsync(employee, existing.Id, CancellationToken.None));
        Assert.Empty(store.Audits);
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

    private sealed class FakeAlertDeviceLookup : IAlertDeviceLookup
    {
        public bool Exists { get; init; } = true;
        public Task<bool> DeviceExistsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult(Exists);
        public Task<IReadOnlyDictionary<Guid, string>> GetDeviceNamesAsync(Guid organizationId, IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
