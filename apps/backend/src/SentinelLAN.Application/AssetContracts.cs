using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record DeviceAssetDetailDto(
    Guid Id,
    string Name,
    string OsVersion,
    string AgentVersion,
    DateTimeOffset? LastSeenAt,
    bool IsOnline,
    Guid? AssignedUserId,
    string? AssignedUserName,
    bool IsRevoked,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? AssetType,
    string? LocationCampus,
    string? LocationBuilding,
    string? LocationFloor,
    string? LocationRoom,
    string AssetStatus,
    DateTimeOffset? PurchaseDate,
    decimal? PurchaseCost,
    DateTimeOffset? WarrantyExpiresAt,
    string? VendorName,
    string? SpecificationsJson,
    HealthScoreResultDto HealthScore,
    RepairVsReplaceResultDto RepairVsReplace
);

public record UpdateAssetProfileRequest(
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? AssetType,
    string? LocationCampus,
    string? LocationBuilding,
    string? LocationFloor,
    string? LocationRoom,
    string? AssetStatus,
    DateTimeOffset? PurchaseDate,
    decimal? PurchaseCost,
    DateTimeOffset? WarrantyExpiresAt,
    string? VendorName,
    string? SpecificationsJson,
    string Reason,
    bool Confirmed
);

public record IncidentDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string Title,
    string? Description,
    string Severity,
    string Status,
    Guid ReportedByUserId,
    string? ReportedByUserName,
    Guid? AssignedTechnicianId,
    string? AssignedTechnicianName,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNotes,
    DateTimeOffset CreatedAt
);

public record CreateIncidentRequest(
    Guid DeviceId,
    string Title,
    string? Description,
    string Severity = "Medium"
);

public record UpdateIncidentStatusRequest(
    string Status,
    Guid? AssignedTechnicianId = null,
    string? ResolutionNotes = null
);

public record WorkOrderDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    Guid? IncidentId,
    string WorkOrderNumber,
    string Title,
    string Type,
    string Priority,
    string Status,
    DateTimeOffset? DueDate,
    DateTimeOffset? CompletedAt,
    double LaborHours,
    decimal PartsCost,
    decimal LaborCost,
    decimal TotalCost,
    string? ChecklistJson,
    string? Notes,
    Guid? AssignedTechnicianId,
    string? AssignedTechnicianName,
    DateTimeOffset CreatedAt
);

public record CreateWorkOrderRequest(
    Guid DeviceId,
    Guid? IncidentId,
    string Title,
    string Type = "Corrective",
    string Priority = "Medium",
    DateTimeOffset? DueDate = null,
    string? ChecklistJson = null,
    string? Notes = null,
    Guid? AssignedTechnicianId = null
);

public record CompleteWorkOrderRequest(
    double LaborHours,
    decimal PartsCost,
    decimal LaborCost,
    string? Notes = null
);

public record AssetLoanDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    Guid BorrowerUserId,
    string? BorrowerUserName,
    string Status,
    DateTimeOffset BorrowedAt,
    DateTimeOffset ExpectedReturnDate,
    DateTimeOffset? ReturnedAt,
    string? ConditionBefore,
    string? ConditionAfter,
    Guid? ApprovedByUserId,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record CreateLoanRequest(
    Guid DeviceId,
    Guid BorrowerUserId,
    DateTimeOffset ExpectedReturnDate,
    string? ConditionBefore = null,
    string? Notes = null
);

public record ReturnLoanRequest(
    string? ConditionAfter = null,
    string? Notes = null
);

public record AssetTimelineItemDto(
    string Id,
    DateTimeOffset Timestamp,
    string EventType,
    string Title,
    string Description,
    string Severity,
    string? Actor = null
);

public record HealthScoreResultDto(
    int Score,
    string Grade,
    double CpuPenalty,
    double RamPenalty,
    double DiskPenalty,
    double OfflinePenalty,
    double AgePenalty,
    double IncidentPenalty,
    IReadOnlyList<string> Recommendations
);

public record RepairVsReplaceResultDto(
    decimal CumulativeMaintenanceCost,
    decimal EstimatedNext3YearsCost,
    decimal? ReplacementCost,
    double MaintenanceToCostRatioPercent,
    string Recommendation,
    string AnalysisSummary
);

public record PublicQrDeviceDto(
    Guid DeviceId,
    string DeviceName,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? AssetType,
    string? Location,
    string AssetStatus,
    bool IsOnline,
    string? AssignedUserName,
    int HealthScore,
    string HealthGrade
);

public interface IAssetManagementService
{
    Task<DeviceAssetDetailDto?> GetDeviceAssetDetailAsync(ActorContext actor, Guid deviceId, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, string Message)> UpdateAssetProfileAsync(ActorContext actor, Guid deviceId, UpdateAssetProfileRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetTimelineItemDto>> GetDeviceTimelineAsync(ActorContext actor, Guid deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentDto>> GetIncidentsAsync(ActorContext actor, Guid? deviceId = null, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, IncidentDto? Incident, string Message)> CreateIncidentAsync(ActorContext actor, CreateIncidentRequest request, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, string Message)> UpdateIncidentStatusAsync(ActorContext actor, Guid incidentId, UpdateIncidentStatusRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrderDto>> GetWorkOrdersAsync(ActorContext actor, Guid? deviceId = null, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, WorkOrderDto? WorkOrder, string Message)> CreateWorkOrderAsync(ActorContext actor, CreateWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, string Message)> CompleteWorkOrderAsync(ActorContext actor, Guid workOrderId, CompleteWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetLoanDto>> GetAssetLoansAsync(ActorContext actor, Guid? deviceId = null, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, AssetLoanDto? Loan, string Message)> CreateAssetLoanAsync(ActorContext actor, CreateLoanRequest request, CancellationToken cancellationToken = default);
    Task<(ManagementResultStatus Status, string Message)> ReturnAssetLoanAsync(ActorContext actor, Guid loanId, ReturnLoanRequest request, CancellationToken cancellationToken = default);
    Task<PublicQrDeviceDto?> GetPublicQrDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
}

public interface IAssetStore
{
    Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken = default);
    Task<Device?> FindDevicePublicAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserNameAsync(Guid organizationId, Guid? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySnapshot>> GetRecentTelemetryAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceCommand>> GetDeviceCommandsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken = default);
    Task UpdateDeviceAsync(Device device, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default);
    Task<IncidentTicket?> FindIncidentAsync(Guid organizationId, Guid incidentId, CancellationToken cancellationToken = default);
    Task<IncidentTicket> CreateIncidentAsync(IncidentTicket incident, CancellationToken cancellationToken = default);
    Task UpdateIncidentAsync(IncidentTicket incident, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default);
    Task<WorkOrder?> FindWorkOrderAsync(Guid organizationId, Guid workOrderId, CancellationToken cancellationToken = default);
    Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task UpdateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetLoan>> GetAssetLoansAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default);
    Task<AssetLoan?> FindAssetLoanAsync(Guid organizationId, Guid loanId, CancellationToken cancellationToken = default);
    Task<AssetLoan> CreateAssetLoanAsync(AssetLoan loan, CancellationToken cancellationToken = default);
    Task UpdateAssetLoanAsync(AssetLoan loan, CancellationToken cancellationToken = default);
    Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}

