using System.Collections.Concurrent;
using System.Threading.Channels;
using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class MonitoringChangeSignal : IMonitoringChangeSignal
{
    private readonly Channel<Guid> pending = Channel.CreateBounded<Guid>(128);
    private readonly ConcurrentDictionary<Guid, byte> queued = new();

    public void RequestCheck(Guid organizationId)
    {
        if (queued.TryAdd(organizationId, 0) && !pending.Writer.TryWrite(organizationId)) queued.TryRemove(organizationId, out _);
    }

    public async Task<Guid> ReadAsync(CancellationToken cancellationToken)
    {
        var organizationId = await pending.Reader.ReadAsync(cancellationToken);
        queued.TryRemove(organizationId, out _);
        return organizationId;
    }
}
