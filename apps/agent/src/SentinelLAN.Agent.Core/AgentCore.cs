using System.Collections.Concurrent;

namespace SentinelLAN.Agent.Core;

public record DeviceIdentity(Guid DeviceId, string DeviceSecret);
public record RemoteCommand(Guid Id, Guid DeviceId, string Type, string Reason, string Nonce, string Signature, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
public record ExecutionResult(bool Succeeded, string Message);

public interface IDeviceIdentityStore { Task<DeviceIdentity?> LoadAsync(CancellationToken cancellationToken); Task SaveAsync(DeviceIdentity identity, CancellationToken cancellationToken); }
public interface ITelemetryCollector { TelemetrySnapshot Collect(); }
public record TelemetrySnapshot(double CpuPercent, double RamPercent, double DiskPercent, string OsVersion, string AgentVersion);
public interface IAgentApi
{
    Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken);
    Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken);
    Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken);
    Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken);
}

public sealed class CommandVerifier
{
    private static readonly HashSet<string> Allowed = ["ShowNotification", "CollectTelemetryNow", "RefreshPolicy", "SimulateLock", "SimulateNetworkIsolation"];
    private readonly ConcurrentDictionary<string, byte> _seenNonces = new();

    public bool TryAccept(RemoteCommand command, Guid expectedDeviceId, DateTimeOffset now, Func<RemoteCommand, bool> verifySignature, out string reason)
    {
        if (command.DeviceId != expectedDeviceId) { reason = "Wrong device"; return false; }
        if (!Allowed.Contains(command.Type)) { reason = "Unsupported type"; return false; }
        if (now < command.IssuedAt.AddMinutes(-1) || now >= command.ExpiresAt) { reason = "Expired or issued in the future"; return false; }
        if (!verifySignature(command)) { reason = "Invalid signature"; return false; }
        if (!_seenNonces.TryAdd(command.Nonce, 0)) { reason = "Replay detected"; return false; }
        reason = string.Empty;
        return true;
    }
}

public static class SafeCommandExecutor
{
    public static ExecutionResult Execute(RemoteCommand command) => command.Type switch
    {
        "ShowNotification" => new(true, "Notification simulated in console"),
        "CollectTelemetryNow" => new(true, "Telemetry collection scheduled"),
        "RefreshPolicy" => new(true, "Policy refresh scheduled"),
        "SimulateLock" => new(true, "Lock simulated; operating system unchanged"),
        "SimulateNetworkIsolation" => new(true, "Network isolation simulated; adapter unchanged"),
        _ => new(false, "Unsupported command")
    };
}

public sealed class LocalQueueStore<T>(int capacity)
{
    private readonly ConcurrentQueue<(DateTimeOffset CreatedAt, T Value)> _items = new();
    public void Enqueue(T value)
    {
        _items.Enqueue((DateTimeOffset.UtcNow, value));
        while (_items.Count > capacity) _items.TryDequeue(out _);
    }
    public IReadOnlyList<T> Drain(TimeSpan retention)
    {
        var minimum = DateTimeOffset.UtcNow - retention;
        var values = new List<T>();
        while (_items.TryDequeue(out var item)) if (item.CreatedAt >= minimum) values.Add(item.Value);
        return values;
    }
}
