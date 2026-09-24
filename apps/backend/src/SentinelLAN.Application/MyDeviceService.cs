using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IMyDeviceStore
{
    Task<Device?> FindAssignedDeviceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<TelemetrySnapshot?> GetLatestTelemetryAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<string?> GetAppliedPolicyNameAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IncidentTicket>> GetUserDeviceIncidentsAsync(Guid organizationId, Guid deviceId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TelemetrySnapshot>> GetDeviceTelemetryHistoryAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLog>> GetDeviceAuditActionsAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken);
    void AddIncident(IncidentTicket incident);
    void AddAudit(AuditLog auditLog);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}

public interface IMyDeviceService
{
    Task<MyDeviceDto?> GetMyDeviceAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySnapshotDto>> GetMyDeviceTelemetryAsync(ActorContext actor, int limit = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentDto>> GetMyDeviceIncidentsAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, IncidentDto? Incident, string Message)> ReportIncidentAsync(
        ActorContext actor, ReportMyDeviceIncidentRequest request, CancellationToken cancellationToken = default);
}

public sealed class MyDeviceService(IMyDeviceStore store) : IMyDeviceService
{
    private static readonly PrivacyManifestDto Manifest = new(
        CollectedTechnicalData:
        [
            "Trạng thái kết nối trực tuyến (Online / Offline)",
            "Thời điểm nhận tín hiệu gần nhất (Last Seen UTC / Local)",
            "Thông số phần cứng kỹ thuật: % CPU, % RAM, % Dung lượng ổ đĩa",
            "Phiên bản hệ điều hành (OS Version) và phiên bản SentinelLAN Agent",
            "Chính sách bảo mật áp dụng (thời gian khóa màn hình khi không hoạt động, chế độ cổng USB)"
        ],
        StrictlyProhibitedData:
        [
            "TUYỆT ĐỐI KHÔNG ghi nhận phím gõ bàn phím (No Keylogger)",
            "TUYỆT ĐỐI KHÔNG chụp ảnh hoặc quay màn hình máy tính (No Screen Capture)",
            "TUYỆT ĐỐI KHÔNG truy cập camera hoặc microphone máy tính (No Webcam/Microphone Access)",
            "TUYỆT ĐỐI KHÔNG đọc nội dung tệp tin hoặc dữ liệu cá nhân (No Personal File Access)",
            "TUYỆT ĐỐI KHÔNG theo dõi lịch sử duyệt web hoặc lưu lượng mạng cá nhân (No Web History / Traffic Sniffing)",
            "TUYỆT ĐỐI KHÔNG thu thập mật khẩu hoặc thông tin đăng nhập của người dùng (No Credential Harvesting)",
            "TUYỆT ĐỐI KHÔNG thực thi shell / PowerShell tùy ý hoặc hành vi phá hoại (No Arbitrary Remote Execution)"
        ],
        AgentPermissions:
        [
            "Chỉ đọc telemetry kỹ thuật tối thiểu theo danh sách công khai",
            "Chỉ nhận các lệnh đã định nghĩa, ký số, có lý do và thời hạn",
            "Không chạy với quyền quản trị nếu tác vụ không bắt buộc"
        ],
        DataRetentionDays: 30
    );

    public async Task<MyDeviceDto?> GetMyDeviceAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        var device = await store.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, cancellationToken);
        if (device is null || device.IsRevoked) return null;

        var now = DateTimeOffset.UtcNow;
        var deviceDto = new DeviceDto(
            device.Id,
            device.Name,
            device.OsVersion,
            device.AgentVersion,
            device.LastSeenAt,
            device.IsOnline(now),
            device.AssignedUserId,
            device.IsRevoked
        );

        var latestTelemetry = await store.GetLatestTelemetryAsync(actor.OrganizationId, device.Id, cancellationToken);
        TelemetrySnapshotDto? telemetryDto = latestTelemetry is null
            ? null
            : new TelemetrySnapshotDto(latestTelemetry.Id, latestTelemetry.DeviceId, latestTelemetry.CpuPercent, latestTelemetry.RamPercent, latestTelemetry.DiskPercent, latestTelemetry.CreatedAt);

        var appliedPolicy = await store.GetAppliedPolicyNameAsync(actor.OrganizationId, device.Id, cancellationToken);
        var incidents = await store.GetUserDeviceIncidentsAsync(actor.OrganizationId, device.Id, actor.UserId, cancellationToken);
        var incidentDtos = incidents.Select(i => new IncidentDto(
            i.Id,
            i.DeviceId,
            device.Name,
            i.Title,
            i.Description,
            i.Severity,
            i.Status,
            i.ReportedByUserId,
            null,
            i.AssignedTechnicianId,
            null,
            i.ResolvedAt,
            i.ResolutionNotes,
            i.CreatedAt
        )).ToList();

        var recentAudits = await store.GetDeviceAuditActionsAsync(actor.OrganizationId, device.Id, 20, cancellationToken);
        var actionDtos = recentAudits.Select(a => new EmployeeDeviceActionDto(
            a.Action,
            a.Reason,
            a.Outcome,
            a.CreatedAt
        )).ToList();

        var location = string.Join(" · ", new[]
        {
            device.LocationCampus,
            device.LocationBuilding,
            device.LocationFloor,
            device.LocationRoom
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new MyDeviceDto(
            deviceDto,
            appliedPolicy,
            telemetryDto,
            incidentDtos,
            actionDtos,
            Manifest,
            device.SerialNumber,
            device.Manufacturer,
            device.Model,
            device.AssetType,
            string.IsNullOrWhiteSpace(location) ? null : location,
            device.CreatedAt);
    }

    public async Task<IReadOnlyList<TelemetrySnapshotDto>> GetMyDeviceTelemetryAsync(ActorContext actor, int limit = 10, CancellationToken cancellationToken = default)
    {
        var device = await store.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, cancellationToken);
        if (device is null || device.IsRevoked) return [];

        var items = await store.GetDeviceTelemetryHistoryAsync(actor.OrganizationId, device.Id, Math.Clamp(limit, 1, 50), cancellationToken);
        return items.Select(t => new TelemetrySnapshotDto(t.Id, t.DeviceId, t.CpuPercent, t.RamPercent, t.DiskPercent, t.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<IncidentDto>> GetMyDeviceIncidentsAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        var device = await store.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, cancellationToken);
        if (device is null || device.IsRevoked) return [];

        var incidents = await store.GetUserDeviceIncidentsAsync(actor.OrganizationId, device.Id, actor.UserId, cancellationToken);
        return incidents.Select(i => new IncidentDto(
            i.Id,
            i.DeviceId,
            device.Name,
            i.Title,
            i.Description,
            i.Severity,
            i.Status,
            i.ReportedByUserId,
            null,
            i.AssignedTechnicianId,
            null,
            i.ResolvedAt,
            i.ResolutionNotes,
            i.CreatedAt
        )).ToList();
    }

    public async Task<(ManagementResultStatus Status, IncidentDto? Incident, string Message)> ReportIncidentAsync(
        ActorContext actor, ReportMyDeviceIncidentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length is < 3 or > 200 ||
            request.Description?.Length > 4000 || request.Severity is not ("Low" or "Medium" or "High" or "Critical"))
            return (ManagementResultStatus.Invalid, null, "Incident title, description or severity is invalid.");

        var device = await store.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, cancellationToken);
        if (device is null || device.IsRevoked)
            return (ManagementResultStatus.NotFound, null, "No active device is currently assigned to your account.");

        var severity = request.Severity;

        var incident = new IncidentTicket
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = device.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Severity = severity,
            Status = "Open",
            ReportedByUserId = actor.UserId
        };

        store.AddIncident(incident);

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "IncidentReportedByEmployee",
            Reason = $"Employee reported incident: {incident.Title}",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken))
            return (ManagementResultStatus.Conflict, null, "Failed to record incident due to concurrency conflict.");

        var dto = new IncidentDto(
            incident.Id,
            incident.DeviceId,
            device.Name,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.ReportedByUserId,
            null,
            null,
            null,
            null,
            null,
            incident.CreatedAt
        );

        return (ManagementResultStatus.Succeeded, dto, "Incident reported successfully.");
    }
}
