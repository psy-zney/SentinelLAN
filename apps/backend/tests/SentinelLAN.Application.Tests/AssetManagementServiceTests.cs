using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class AssetManagementServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid adminId = Guid.NewGuid();
    private readonly Guid employeeId = Guid.NewGuid();
    private readonly Guid technicianId = Guid.NewGuid();

    [Fact]
    public async Task GetDeviceAssetDetailCalculatesCorrectHealthAndRepairVsReplace()
    {
        var store = new FakeAssetStore();
        var device = new Device
        {
            OrganizationId = organizationId,
            Name = "TEST-PC",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            LastSeenAt = DateTimeOffset.UtcNow,
            PurchaseDate = DateTimeOffset.UtcNow.AddYears(-1),
            PurchaseCost = 1000m
        };
        store.Devices.Add(device);

        var service = new AssetManagementService(store);
        var actor = new ActorContext(adminId, organizationId, Roles.Admin);

        var result = await service.GetDeviceAssetDetailAsync(actor, device.Id);

        Assert.NotNull(result);
        Assert.True(result.IsOnline);
        Assert.Equal(100, result.HealthScore.Score);
        Assert.Equal("Excellent", result.HealthScore.Grade);
        Assert.Equal("Keep & Maintain", result.RepairVsReplace.Recommendation);
    }

    [Fact]
    public async Task HealthScoreDeductsPenaltiesWhenOfflineAndHasIncidents()
    {
        var store = new FakeAssetStore();
        var device = new Device
        {
            OrganizationId = organizationId,
            Name = "OFFLINE-PC",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-10), // Offline
            PurchaseDate = DateTimeOffset.UtcNow.AddYears(-6) // Over 5 years -> age penalty
        };
        store.Devices.Add(device);

        store.Incidents.Add(new IncidentTicket
        {
            OrganizationId = organizationId,
            DeviceId = device.Id,
            Title = "Broken GPU",
            Severity = "Critical",
            Status = "Open",
            ReportedByUserId = employeeId
        });

        var service = new AssetManagementService(store);
        var actor = new ActorContext(adminId, organizationId, Roles.Admin);

        var result = await service.GetDeviceAssetDetailAsync(actor, device.Id);

        Assert.NotNull(result);
        Assert.False(result.IsOnline);
        Assert.True(result.HealthScore.Score < 60);
        Assert.Equal(25.0, result.HealthScore.OfflinePenalty);
        Assert.Equal(15.0, result.HealthScore.AgePenalty);
        Assert.Equal(25.0, result.HealthScore.IncidentPenalty);
    }

    [Fact]
    public async Task EmployeeCanReportIncidentOnAssignedDeviceOnly()
    {
        var store = new FakeAssetStore();
        var myDevice = new Device
        {
            OrganizationId = organizationId,
            Name = "MY-PC",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            AssignedUserId = employeeId
        };
        var otherDevice = new Device
        {
            OrganizationId = organizationId,
            Name = "OTHER-PC",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            AssignedUserId = Guid.NewGuid()
        };
        store.Devices.Add(myDevice);
        store.Devices.Add(otherDevice);

        var service = new AssetManagementService(store);
        var employeeActor = new ActorContext(employeeId, organizationId, Roles.Employee);

        var allowed = await service.CreateIncidentAsync(employeeActor, new CreateIncidentRequest(myDevice.Id, "Keyboard broken", "Some keys do not work"));
        var forbidden = await service.CreateIncidentAsync(employeeActor, new CreateIncidentRequest(otherDevice.Id, "Hacked", "Cannot report on other device"));

        Assert.Equal(ManagementResultStatus.Succeeded, allowed.Status);
        Assert.NotNull(allowed.Incident);
        Assert.Equal(ManagementResultStatus.Forbidden, forbidden.Status);
    }

    [Fact]
    public async Task WorkOrderCompletionResolvesIncidentAndRecalculatesTco()
    {
        var store = new FakeAssetStore();
        var device = new Device
        {
            OrganizationId = organizationId,
            Name = "MAINT-PC",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            PurchaseCost = 1000m
        };
        store.Devices.Add(device);

        var incident = new IncidentTicket
        {
            OrganizationId = organizationId,
            DeviceId = device.Id,
            Title = "Overheating",
            Severity = "High",
            Status = "Open",
            ReportedByUserId = employeeId
        };
        store.Incidents.Add(incident);

        var service = new AssetManagementService(store);
        var techActor = new ActorContext(technicianId, organizationId, Roles.Technician);

        var (createStatus, wo, _) = await service.CreateWorkOrderAsync(techActor, new CreateWorkOrderRequest(
            device.Id, incident.Id, "Fix heatsink", "Corrective", "High"
        ));

        Assert.Equal(ManagementResultStatus.Succeeded, createStatus);
        Assert.NotNull(wo);

        var (completeStatus, _) = await service.CompleteWorkOrderAsync(techActor, wo.Id, new CompleteWorkOrderRequest(
            2.0, 500m, 150m, "Replaced fans and thermal compound"
        ));

        Assert.Equal(ManagementResultStatus.Succeeded, completeStatus);

        // Verify incident resolved
        var updatedInc = await store.FindIncidentAsync(organizationId, incident.Id);
        Assert.Equal("Resolved", updatedInc!.Status);

        // Verify repair vs replace reflects high cost (>650 / 1000 = 65%)
        var detail = await service.GetDeviceAssetDetailAsync(techActor, device.Id);
        Assert.Equal(650m, detail!.RepairVsReplace.CumulativeMaintenanceCost);
        Assert.Equal("Replace Immediately", detail.RepairVsReplace.Recommendation);
    }

    [Fact]
    public async Task TimelineAggregatesEventsInDescendingOrder()
    {
        var store = new FakeAssetStore();
        var device = new Device
        {
            OrganizationId = organizationId,
            Name = "TIMELINE-PC",
            OsVersion = "Windows 11",
            AgentVersion = "0.1.0",
            PurchaseDate = DateTimeOffset.UtcNow.AddDays(-10)
        };
        store.Devices.Add(device);

        store.Incidents.Add(new IncidentTicket
        {
            OrganizationId = organizationId,
            DeviceId = device.Id,
            Title = "Screen flicker",
            ReportedByUserId = employeeId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        var service = new AssetManagementService(store);
        var actor = new ActorContext(adminId, organizationId, Roles.Admin);

        var timeline = await service.GetDeviceTimelineAsync(actor, device.Id);

        Assert.NotEmpty(timeline);
        for (int i = 0; i < timeline.Count - 1; i++)
        {
            Assert.True(timeline[i].Timestamp >= timeline[i + 1].Timestamp);
        }
    }

    [Fact]
    public async Task IncidentAssignmentRejectsCrossTenantTechnicianAndInvalidStatus()
    {
        var store = new FakeAssetStore();
        var device = NewDevice();
        store.Devices.Add(device);
        var incident = new IncidentTicket { OrganizationId = organizationId, DeviceId = device.Id, Title = "Fault", ReportedByUserId = employeeId };
        store.Incidents.Add(incident);
        store.Users.Add(new User { OrganizationId = Guid.NewGuid(), Email = "other@example.test", DisplayName = "Other", Role = Roles.Technician, PasswordHash = "unused" });
        var service = new AssetManagementService(store);
        var actor = new ActorContext(adminId, organizationId, Roles.Admin);

        var invalidStatus = await service.UpdateIncidentStatusAsync(actor, incident.Id, new UpdateIncidentStatusRequest("Arbitrary"));
        var crossTenant = await service.UpdateIncidentStatusAsync(actor, incident.Id,
            new UpdateIncidentStatusRequest("InProgress", store.Users[0].Id));

        Assert.Equal(ManagementResultStatus.Invalid, invalidStatus.Status);
        Assert.Equal(ManagementResultStatus.Invalid, crossTenant.Status);
        Assert.Equal("Open", incident.Status);
        Assert.Empty(store.Audits);
    }

    [Fact]
    public async Task WorkOrderAndLoanValidateTenantReferencesAndPriority()
    {
        var store = new FakeAssetStore();
        var device = NewDevice();
        store.Devices.Add(device);
        store.Incidents.Add(new IncidentTicket { OrganizationId = Guid.NewGuid(), DeviceId = device.Id, Title = "Other tenant", ReportedByUserId = employeeId });
        var actor = new ActorContext(adminId, organizationId, Roles.Admin);
        var service = new AssetManagementService(store);

        var badPriority = await service.CreateWorkOrderAsync(actor,
            new CreateWorkOrderRequest(device.Id, null, "Fix device", Priority: "Immediate"));
        var crossTenantIncident = await service.CreateWorkOrderAsync(actor,
            new CreateWorkOrderRequest(device.Id, store.Incidents[0].Id, "Fix device"));
        var crossTenantBorrower = await service.CreateAssetLoanAsync(actor,
            new CreateLoanRequest(device.Id, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(ManagementResultStatus.Invalid, badPriority.Status);
        Assert.Equal(ManagementResultStatus.NotFound, crossTenantIncident.Status);
        Assert.Equal(ManagementResultStatus.Invalid, crossTenantBorrower.Status);
        Assert.Empty(store.WorkOrders);
        Assert.Empty(store.Loans);
        Assert.Empty(store.Audits);
    }

    [Fact]
    public async Task WorkOrderQueueShowsActiveCriticalWorkBeforeCompletedAndLowPriority()
    {
        var store = new FakeAssetStore();
        var device = NewDevice();
        store.Devices.Add(device);
        store.WorkOrders.AddRange(
            new WorkOrder { OrganizationId = organizationId, DeviceId = device.Id, WorkOrderNumber = "WO-LOW", Title = "Low", Priority = "Low", Status = "Scheduled" },
            new WorkOrder { OrganizationId = organizationId, DeviceId = device.Id, WorkOrderNumber = "WO-DONE", Title = "Done", Priority = "Critical", Status = "Completed" },
            new WorkOrder { OrganizationId = organizationId, DeviceId = device.Id, WorkOrderNumber = "WO-URGENT", Title = "Urgent", Priority = "Critical", Status = "Scheduled" });

        var result = await new AssetManagementService(store)
            .GetWorkOrdersAsync(new ActorContext(adminId, organizationId, Roles.Admin));

        Assert.Equal(["WO-URGENT", "WO-LOW", "WO-DONE"], result.Select(item => item.WorkOrderNumber));
    }

    private Device NewDevice() => new()
    {
        OrganizationId = organizationId,
        Name = "TEST-PC",
        OsVersion = "Windows 11",
        AgentVersion = "test"
    };
}

internal sealed class FakeAssetStore : IAssetStore
{
    public List<Device> Devices { get; } = [];
    public List<IncidentTicket> Incidents { get; } = [];
    public List<WorkOrder> WorkOrders { get; } = [];
    public List<AssetLoan> Loans { get; } = [];
    public List<TelemetrySnapshot> Telemetry { get; } = [];
    public List<DeviceCommand> Commands { get; } = [];
    public List<AuditLog> Audits { get; } = [];
    public List<User> Users { get; } = [];

    public Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Devices.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == deviceId));

    public Task<Device?> FindDevicePublicAsync(Guid deviceId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Devices.FirstOrDefault(x => x.Id == deviceId));

    public Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == userId));

    public Task<string?> GetUserNameAsync(Guid organizationId, Guid? userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == userId)?.DisplayName);

    public Task<IReadOnlyList<TelemetrySnapshot>> GetRecentTelemetryAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TelemetrySnapshot>>(Telemetry.Where(x => x.OrganizationId == organizationId && x.DeviceId == deviceId).Take(limit).ToList());

    public Task<IReadOnlyList<DeviceCommand>> GetDeviceCommandsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DeviceCommand>>(Commands.Where(x => x.OrganizationId == organizationId && x.DeviceId == deviceId).ToList());

    public Task UpdateDeviceAsync(Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IncidentTicket>>(Incidents.Where(x => x.OrganizationId == organizationId && (!deviceId.HasValue || x.DeviceId == deviceId.Value)).ToList());

    public Task<IncidentTicket?> FindIncidentAsync(Guid organizationId, Guid incidentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Incidents.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == incidentId));

    public Task<IncidentTicket> CreateIncidentAsync(IncidentTicket incident, CancellationToken cancellationToken = default)
    {
        Incidents.Add(incident);
        return Task.FromResult(incident);
    }

    public Task UpdateIncidentAsync(IncidentTicket incident, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkOrder>>(WorkOrders.Where(x => x.OrganizationId == organizationId && (!deviceId.HasValue || x.DeviceId == deviceId.Value)).ToList());

    public Task<WorkOrder?> FindWorkOrderAsync(Guid organizationId, Guid workOrderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(WorkOrders.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == workOrderId));

    public Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        WorkOrders.Add(workOrder);
        return Task.FromResult(workOrder);
    }

    public Task UpdateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AssetLoan>> GetAssetLoansAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssetLoan>>(Loans.Where(x => x.OrganizationId == organizationId && (!deviceId.HasValue || x.DeviceId == deviceId.Value)).ToList());

    public Task<AssetLoan?> FindAssetLoanAsync(Guid organizationId, Guid loanId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Loans.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == loanId));

    public Task<AssetLoan> CreateAssetLoanAsync(AssetLoan loan, CancellationToken cancellationToken = default)
    {
        Loans.Add(loan);
        return Task.FromResult(loan);
    }

    public Task UpdateAssetLoanAsync(AssetLoan loan, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        Audits.Add(auditLog);
        return Task.CompletedTask;
    }
}
