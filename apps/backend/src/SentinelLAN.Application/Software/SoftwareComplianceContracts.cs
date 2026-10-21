using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record SoftwareDetectionRuleDto(Guid Id, string Name, string Source, Guid? ProductCode, Guid? UpgradeCode, string? RequiredVersion, Guid Revision);
public sealed record AgentSoftwareObservationDto(Guid RuleId, string Source, string Detection, string? InstalledVersion, DateTimeOffset CapturedAt, Guid RuleRevision);
public sealed record SoftwareComplianceDto(Guid RuleId, string Name, string Source, string Status, string? InstalledVersion, string? RequiredVersion, DateTimeOffset? CapturedAt);
public sealed record ProhibitedSoftwareRuleDto(Guid Id, string Name, Guid? ProductCode, Guid? UpgradeCode, bool IsActive, Guid Revision);
public sealed record SaveProhibitedSoftwareRuleRequest(string Name, Guid? ProductCode, Guid? UpgradeCode, bool IsActive,
    Guid Revision, string Reason, bool Confirmed, Guid Nonce, DateTimeOffset ExpiresAt);

// The self-service catalog publishes a detection-only view to this module.
public interface ISoftwareCatalogDetectionReader
{
    Task<IReadOnlyList<SoftwareDetectionRuleDto>> GetRulesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetKnownIdsAsync(Guid organizationId, CancellationToken cancellationToken);
}
public interface ISoftwareComplianceStore
{
    Task<IReadOnlyList<ProhibitedSoftwareRule>> GetProhibitedRulesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<ProhibitedSoftwareRule?> FindRuleAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceSoftwareObservation>> GetObservationsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    void Add(ProhibitedSoftwareRule rule);
    void Add(DeviceSoftwareObservation observation);
}
public interface ISoftwareDetectionQuota
{
    Task<bool> CanUseAsync(Guid organizationId, Guid id, string source, bool enabled, CancellationToken cancellationToken);
}
