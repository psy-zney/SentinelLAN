namespace SentinelLAN.Agent.Core;

public interface IAgentWakeSignal
{
    Task RunAsync(DeviceIdentity identity, CancellationToken ct);
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct);
}
