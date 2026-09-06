namespace SentinelLAN.Agent.Core;

public sealed class CpuUsageSampler
{
    private (ulong Idle, ulong Total)? _previous;

    public double Sample(ulong idle, ulong total)
    {
        var previous = _previous;
        _previous = (idle, total);
        if (previous is null || total <= previous.Value.Total || idle < previous.Value.Idle) return 0;
        return Math.Clamp(100d * (1 - (idle - previous.Value.Idle) / (double)(total - previous.Value.Total)), 0, 100);
    }
}
