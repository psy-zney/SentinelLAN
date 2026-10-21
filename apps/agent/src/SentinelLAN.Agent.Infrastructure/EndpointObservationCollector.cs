using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class EndpointObservationCollector(IEndpointPlatformProbe probe, TimeProvider? timeProvider = null)
    : IEndpointObservationCollector, IHardwareInventoryCollector, IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);
    private EndpointObservations? cached;
    private DateTimeOffset nextRead;
    private DateTimeOffset? lastInventory;

    public async Task<HardwareInventory?> GetInventoryAsync(CancellationToken ct) => (await ReadAsync(ct)).Inventory;
    public async Task<EndpointObservations> CollectAsync(CancellationToken ct)
    {
        var value = await ReadAsync(ct);
        var inventory = value.Inventory?.CapturedAt == lastInventory ? null : value.Inventory;
        lastInventory = value.Inventory?.CapturedAt;
        return value with { Inventory = inventory };
    }
    public void Invalidate() => nextRead = default;
    public void Dispose() => gate.Dispose();

    private async Task<EndpointObservations> ReadAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (cached is null || clock.GetUtcNow() >= nextRead)
            {
                cached = await probe.ReadAsync(ct);
                nextRead = clock.GetUtcNow().AddMinutes(1);
            }
            return cached;
        }
        finally { gate.Release(); }
    }
}
