using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class SoftwareObservationCollector(IAgentApi api, ISoftwareProductProbe probe, TimeProvider? timeProvider = null) : ISoftwareObservationCollector
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private DateTimeOffset nextScan;
    public void Invalidate() => nextScan = default;

    public async Task<IReadOnlyList<SoftwareObservation>?> CollectAsync(DeviceIdentity identity, CancellationToken ct)
    {
        if (clock.GetUtcNow() < nextScan) return null;
        nextScan = clock.GetUtcNow().AddMinutes(1);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            var rules = await api.GetSoftwareRulesAsync(identity, deadline.Token);
            if (rules.Count > 256 || rules.Any(rule => rule.Id == Guid.Empty || rule.Revision == Guid.Empty || rule.Source is not ("Catalog" or "Prohibited") ||
                rule.ProductCode is null && rule.UpgradeCode is null || rule.ProductCode == Guid.Empty || rule.UpgradeCode == Guid.Empty)) return null;
            var result = new List<SoftwareObservation>();
            foreach (var rule in rules)
            {
                if (deadline.IsCancellationRequested) break;
                var sample = probe.Read(rule);
                result.Add(new(rule.Id, rule.Source, sample.Detection, sample.InstalledVersion, clock.GetUtcNow(), rule.Revision));
            }
            nextScan = clock.GetUtcNow().AddMinutes(15);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is HttpRequestException or JsonException or IOException or OperationCanceledException) { return null; }
    }
}
