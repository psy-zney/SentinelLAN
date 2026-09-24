using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class MyDeviceServiceTests
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly Guid employeeId = Guid.NewGuid();
    private readonly FakeMyDeviceStore store = new();

    [Fact]
    public async Task GetMyDeviceReturnsAssignedDeviceAndPrivacyData()
    {
        var device = new Device
        {
            OrganizationId = orgId,
            Name = "MY-LAPTOP",
            OsVersion = "Windows 11 24H2",
            AgentVersion = "0.1.0",
            AssignedUserId = employeeId
        };
        store.Devices.Add(device);
        store.LatestTelemetry = new TelemetrySnapshot
        {
            OrganizationId = orgId,
            DeviceId = device.Id,
            CpuPercent = 15.5,
            RamPercent = 42.0,
            DiskPercent = 65.2
        };
        store.AppliedPolicy = "Standard Office Protection";

        var service = new MyDeviceService(store);
        var actor = new ActorContext(employeeId, orgId, Roles.Employee);

        var result = await service.GetMyDeviceAsync(actor, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("MY-LAPTOP", result!.Device.Name);
        Assert.Equal("Standard Office Protection", result.AppliedPolicy);
        Assert.NotNull(result.LatestTelemetry);
        Assert.Equal(15.5, result.LatestTelemetry!.CpuPercent);

        // Privacy manifest guarantees
        Assert.NotEmpty(result.PrivacyManifest.CollectedTechnicalData);
        Assert.NotEmpty(result.PrivacyManifest.StrictlyProhibitedData);
        Assert.Contains(result.PrivacyManifest.StrictlyProhibitedData, item => item.Contains("Keylogger", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PrivacyManifest.StrictlyProhibitedData, item => item.Contains("Screen Capture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetMyDeviceReturnsNullWithoutAssignment()
    {
        var service = new MyDeviceService(store);
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.Employee);

        var result = await service.GetMyDeviceAsync(actor, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReportIncidentForAssignedDeviceCreatesAudit()
    {
        var device = new Device
        {
            OrganizationId = orgId,
            Name = "WORKSTATION-01",
            OsVersion = "Win11",
            AgentVersion = "1.0",
            AssignedUserId = employeeId
        };
        store.Devices.Add(device);

        var service = new MyDeviceService(store);
        var actor = new ActorContext(employeeId, orgId, Roles.Employee);

        var result = await service.ReportIncidentAsync(actor, new ReportMyDeviceIncidentRequest("Blue screen on boot", "Error code 0x0000001", "High", "employee-report-1"), CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Succeeded, result.Status);
        Assert.NotNull(result.Incident);
        Assert.Equal("Blue screen on boot", result.Incident!.Title);
        Assert.Equal("High", result.Incident.Severity);
        Assert.Equal(employeeId, result.Incident.ReportedByUserId);

        var audit = store.Audits.Single(a => a.Action == "IncidentReportedByEmployee");
        Assert.Equal(device.Id, audit.DeviceId);
    }

    [Fact]
    public async Task ReportIncidentReplaysSameKeyAndRejectsChangedPayload()
    {
        var device = new Device { OrganizationId = orgId, Name = "WORKSTATION-02", OsVersion = "Win11", AgentVersion = "1.0", AssignedUserId = employeeId };
        store.Devices.Add(device);
        var service = new MyDeviceService(store);
        var actor = new ActorContext(employeeId, orgId, Roles.Employee);
        var request = new ReportMyDeviceIncidentRequest("Blue screen on boot", "Error code 0x0000001", "High", "retry-key-1");

        var first = await service.ReportIncidentAsync(actor, request, CancellationToken.None);
        var replay = await service.ReportIncidentAsync(actor, request, CancellationToken.None);
        var conflict = await service.ReportIncidentAsync(actor, request with { Title = "Different incident title" }, CancellationToken.None);

        Assert.Equal(ManagementResultStatus.Succeeded, first.Status);
        Assert.Equal(first.Incident!.Id, replay.Incident!.Id);
        Assert.Single(store.Incidents);
        Assert.Single(store.Audits);
        Assert.Equal(ManagementResultStatus.Conflict, conflict.Status);
    }

    [Fact]
    public async Task ReportIncidentRejectsMissingAssignmentOrInvalidTitle()
    {
        var service = new MyDeviceService(store);
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.Employee);

        var noDevice = await service.ReportIncidentAsync(actor, new ReportMyDeviceIncidentRequest("Valid title", "desc", IdempotencyKey: "missing-device-key"), CancellationToken.None);
        Assert.Equal(ManagementResultStatus.NotFound, noDevice.Status);

        var missingKey = await service.ReportIncidentAsync(actor, new ReportMyDeviceIncidentRequest("Valid title", "desc"), CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Invalid, missingKey.Status);

        var invalidTitle = await service.ReportIncidentAsync(actor, new ReportMyDeviceIncidentRequest("a", "desc", IdempotencyKey: "invalid-title-key"), CancellationToken.None);
        Assert.Equal(ManagementResultStatus.Invalid, invalidTitle.Status);
    }

    private sealed class FakeMyDeviceStore : IMyDeviceStore
    {
        public List<Device> Devices { get; } = [];
        public List<IncidentTicket> Incidents { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public TelemetrySnapshot? LatestTelemetry { get; set; }
        public string? AppliedPolicy { get; set; }

        public Task<Device?> FindAssignedDeviceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.OrganizationId == organizationId && d.AssignedUserId == userId && !d.IsRevoked));

        public Task<TelemetrySnapshot?> GetLatestTelemetryAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(LatestTelemetry);

        public Task<string?> GetAppliedPolicyNameAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(AppliedPolicy);

        public Task<IReadOnlyList<IncidentTicket>> GetUserDeviceIncidentsAsync(Guid organizationId, Guid deviceId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncidentTicket>>(Incidents.Where(i => i.OrganizationId == organizationId && i.DeviceId == deviceId && i.ReportedByUserId == userId).ToList());

        public Task<IReadOnlyList<TelemetrySnapshot>> GetDeviceTelemetryHistoryAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TelemetrySnapshot>>(LatestTelemetry is not null ? [LatestTelemetry] : []);

        public Task<IReadOnlyList<AuditLog>> GetDeviceAuditActionsAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditLog>>(Audits.Where(a => a.OrganizationId == organizationId && a.DeviceId == deviceId).Take(limit).ToList());

        public Task<IncidentTicket?> FindIncidentByIdempotencyKeyAsync(Guid organizationId, Guid reportedByUserId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Incidents.FirstOrDefault(x => x.OrganizationId == organizationId && x.ReportedByUserId == reportedByUserId && x.IdempotencyKey == idempotencyKey));

        public Task<IncidentCreateResult> CreateIncidentAsync(IncidentTicket incident, AuditLog auditLog, CancellationToken cancellationToken)
        {
            var existing = Incidents.FirstOrDefault(x => x.OrganizationId == incident.OrganizationId && x.ReportedByUserId == incident.ReportedByUserId && x.IdempotencyKey == incident.IdempotencyKey);
            if (existing is not null)
                return Task.FromResult(new IncidentCreateResult(existing, true, existing.RequestFingerprint != incident.RequestFingerprint));
            Incidents.Add(incident);
            Audits.Add(auditLog);
            return Task.FromResult(new IncidentCreateResult(incident, false, false));
        }
    }
}
