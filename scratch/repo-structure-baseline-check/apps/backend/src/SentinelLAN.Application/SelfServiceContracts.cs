using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record SupportRequestDto(Guid Id, Guid DeviceId, string DeviceName, Guid UserId, string UserName,
    string Kind, string Category, string Title, string? Description, bool CanWork, string Status,
    Guid? AssignedTechnicianId, string? AssignedTechnicianName, Guid? CatalogAppId, Guid? CommandId,
    string? CommandStatus, string? CommandMessage, DateTimeOffset? AppointmentAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ApprovalExpiresAt);
public sealed record SupportMessageDto(Guid Id, Guid RequestId, Guid AuthorId, string AuthorName, string Body, DateTimeOffset CreatedAt);
public sealed record SupportAttachmentDto(Guid Id, Guid RequestId, string FileName, string ContentType, int Size, DateTimeOffset CreatedAt);
public sealed record CatalogAppDto(Guid Id, string Name, string Description, string Version, string PackageUrl,
    string Sha256, string PublisherThumbprint, bool IsActive, bool RequiresApproval, DateTimeOffset CreatedAt);
public sealed record AnnouncementDto(Guid Id, string Title, string Body, bool IsOutage, bool RequiresAcknowledgement,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool Acknowledged, bool Affected, DateTimeOffset CreatedAt);
public sealed record EmployeeNotificationDto(Guid Id, string Title, string Body, Guid? RequestId, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);
public sealed record HelpArticleDto(Guid Id, string Category, string Title, IReadOnlyList<string> Steps);
public sealed record SelfServiceTeamMemberDto(Guid Id, string DisplayName);
public sealed record CreateSupportRequest(string Kind, string Category, string Title, string? Description,
    bool CanWork, Guid? CatalogAppId, DateTimeOffset? AppointmentAt, bool Confirmed, string IdempotencyKey);
public sealed record CreateSupportMessage(string Body, string IdempotencyKey);
public sealed record CreateSupportAttachment(string FileName, string ContentType, string Base64);
public sealed record UpdateSupportRequest(string Status, Guid? AssignedTechnicianId, string Reason, bool Confirmed);
public sealed record DecideSupportRequest(bool Approved, string Reason, bool Confirmed);
public sealed record SupportDecisionDto(SupportRequestDto Request, string? Otp, DateTimeOffset? ExpiresAt);
public sealed record RedeemMaintenanceRequest(string Code, bool Confirmed);
public sealed record AgentRedeemMaintenanceRequest(Guid RequestId, string Code);
public sealed record SaveCatalogApp(string Name, string Description, string Version, string PackageUrl,
    string Sha256, string PublisherThumbprint, bool IsActive, bool RequiresApproval, string Reason, bool Confirmed);
public sealed record CreateAnnouncement(string Title, string Body, bool IsOutage, bool RequiresAcknowledgement,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason, bool Confirmed);
public sealed record RegisterPushDevice(string Token, string Platform);
public sealed record DeletePushDevice(string Token);
public sealed record ApprovedPackageSnapshot(string PackageUrl, string Sha256, string PublisherThumbprint);
public sealed record SelfServiceSettings(bool PanicLabEnabled, IReadOnlySet<Guid> PanicLabDeviceIds);

public sealed class SelfServiceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// Stores only the self-service module's data; other module access uses the ports below.
public interface ISelfServiceStore
{
    Task<ISelfServiceWriteScope> BeginWriteAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceRequest>> GetRequestsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<SelfServiceRequest?> FindRequestAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<SelfServiceRequest?> FindIdempotentRequestAsync(Guid organizationId, Guid userId, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceMessage>> GetMessagesAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceAttachment>> GetAttachmentsAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken);
    Task<SelfServiceAttachment?> FindAttachmentAsync(Guid organizationId, Guid requestId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceCatalogApp>> GetCatalogAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<SelfServiceCatalogApp?> FindCatalogAppAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceAnnouncement>> GetAnnouncementsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceAnnouncementReceipt>> GetReceiptsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServiceNotification>> GetNotificationsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<SelfServicePushDevice?> FindPushDeviceAsync(string token, CancellationToken cancellationToken);
    Task<IReadOnlyList<SelfServicePushDevice>> GetPushDevicesAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    void Add<T>(T entity) where T : Entity, ITenantOwned;
    void RemovePushDevice(SelfServicePushDevice device);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ISelfServiceWriteScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface ISelfServiceDirectory
{
    Task<Device?> FindAssignedDeviceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetActiveUsersAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Device>> GetDevicesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetOrganizationIdsAsync(CancellationToken cancellationToken);
}

public interface ISelfServiceCommands
{
    DeviceCommand Queue(Guid organizationId, Guid deviceId, Guid actorId, string type, string reason,
        string parameter, DateTimeOffset now, DateTimeOffset expiresAt);
    Task<(DeviceCommand? Command, CommandResult? Result)> GetAsync(Guid organizationId, Guid commandId, CancellationToken cancellationToken);
}

public interface ISelfServiceAudit
{
    void Record(Guid organizationId, Guid actorId, Guid? deviceId, string action, string reason, string outcome);
    void CriticalAlert(Guid organizationId, Guid deviceId, string message);
}

public interface IMaintenanceCodeProtector
{
    string Generate();
    string Protect(SelfServiceRequest request, string code);
    bool Matches(SelfServiceRequest request, string code);
}
