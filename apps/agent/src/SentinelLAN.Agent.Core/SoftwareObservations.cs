namespace SentinelLAN.Agent.Core;

public sealed record SoftwareDetectionRule(Guid Id, string Name, string Source, Guid? ProductCode, Guid? UpgradeCode, string? RequiredVersion, Guid Revision);
public sealed record SoftwareObservation(Guid RuleId, string Source, string Detection, string? InstalledVersion, DateTimeOffset CapturedAt, Guid RuleRevision);
public interface ISoftwareObservationCollector
{
    Task<IReadOnlyList<SoftwareObservation>?> CollectAsync(DeviceIdentity identity, CancellationToken cancellationToken);
    void Invalidate();
}
