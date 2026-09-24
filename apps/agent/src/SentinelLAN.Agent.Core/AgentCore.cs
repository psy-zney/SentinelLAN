using System.Collections.Concurrent;

namespace SentinelLAN.Agent.Core;

public record DeviceIdentity(Guid DeviceId, string DeviceSecret);
public record RemoteCommand(Guid Id, Guid DeviceId, string Type, string Reason, string Nonce, string Signature, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, Guid OrganizationId = default, Guid IssuedByUserId = default, string? Parameter = null);
public interface ICommandSignatureVerifier { bool IsConfigured { get; } bool Verify(RemoteCommand command); }
public record ExecutionResult(bool Succeeded, string Message);

public interface IDeviceIdentityStore { Task<DeviceIdentity?> LoadAsync(CancellationToken cancellationToken); Task SaveAsync(DeviceIdentity identity, CancellationToken cancellationToken); }
public interface ITelemetryCollector { TelemetrySnapshot Collect(); }
public record TelemetrySnapshot(double CpuPercent, double RamPercent, double DiskPercent, string OsVersion, string AgentVersion);
public record QueuedTelemetry(TelemetrySnapshot Snapshot, string IdempotencyKey);
public interface IAgentApi
{
    Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken);
    Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken);
    Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, string? idempotencyKey, CancellationToken cancellationToken);
    Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken);
    Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken);
}

public sealed class CommandVerifier
{
    private static readonly HashSet<string> Allowed = ["ShowNotification", "CollectTelemetryNow", "RefreshPolicy", "SimulateLock", "SimulateNetworkIsolation", "RestartService"];
    private readonly ConcurrentDictionary<string, byte> _seenNonces = new();

    public bool TryAccept(RemoteCommand command, Guid expectedDeviceId, DateTimeOffset now, Func<RemoteCommand, bool> verifySignature, out string reason)
    {
        if (command.DeviceId != expectedDeviceId) { reason = "Wrong device"; return false; }
        if (!Allowed.Contains(command.Type)) { reason = "Unsupported type"; return false; }
        if (string.IsNullOrWhiteSpace(command.Nonce) || string.IsNullOrWhiteSpace(command.Signature) || command.ExpiresAt <= command.IssuedAt || command.ExpiresAt - command.IssuedAt > TimeSpan.FromMinutes(15)) { reason = "Invalid command envelope"; return false; }
        if (now < command.IssuedAt.AddMinutes(-1) || now >= command.ExpiresAt) { reason = "Expired or issued in the future"; return false; }
        if (!verifySignature(command)) { reason = "Invalid signature"; return false; }
        if (!_seenNonces.TryAdd(command.Nonce, 0)) { reason = "Replay detected"; return false; }
        reason = string.Empty;
        return true;
    }
}

public static class SafeCommandExecutor
{
    private static readonly HashSet<string> AllowedServices = ["docker", "nginx", "caddy"];

    public static ExecutionResult Execute(RemoteCommand command, bool allowRealExecution = false)
    {
        return command.Type switch
        {
            "ShowNotification" => new(true, $"Notification simulated; nothing was displayed: {command.Reason}"),
            "CollectTelemetryNow" => new(true, "Telemetry collection request simulated; no immediate collection was performed"),
            "RefreshPolicy" => new(true, "Policy refresh request simulated; no policy was applied"),
            "SimulateLock" => new(true, "Lock simulated; operating system unchanged"),
            "SimulateNetworkIsolation" => new(true, "Network isolation simulated; adapter unchanged"),
            "RestartService" => ExecuteRestartService(command.Parameter),
            _ => new(false, "Unsupported command")
        };
    }

    private static ExecutionResult ExecuteRestartService(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return new(false, "Service name parameter is required for RestartService");

        var normalized = serviceName.Trim().ToLowerInvariant();
        if (!AllowedServices.Contains(normalized))
            return new(false, $"Service '{serviceName}' is not in the safe allow-list (allowed: {string.Join(", ", AllowedServices)})");

        return new(true, $"Service '{normalized}' restart simulated; service state unchanged");
    }
}


public class ResilientOfflineQueue<T>(int capacity = 100)
{
    private readonly ConcurrentQueue<(DateTimeOffset CreatedAt, T Value)> _items = new();
    public int Count => _items.Count;

    public void Enqueue(T value)
    {
        _items.Enqueue((DateTimeOffset.UtcNow, value));
        while (_items.Count > capacity) _items.TryDequeue(out _);
    }

    public bool TryPeek(TimeSpan retention, out T? value)
    {
        var minimum = DateTimeOffset.UtcNow - retention;
        while (_items.TryPeek(out var item))
        {
            if (item.CreatedAt >= minimum)
            {
                value = item.Value;
                return true;
            }
            _items.TryDequeue(out _);
        }
        value = default;
        return false;
    }

    public bool TryDequeue(out T? value)
    {
        if (_items.TryDequeue(out var item))
        {
            value = item.Value;
            return true;
        }
        value = default;
        return false;
    }

    public IReadOnlyList<T> Drain(TimeSpan retention)
    {
        var minimum = DateTimeOffset.UtcNow - retention;
        var values = new List<T>();
        while (_items.TryDequeue(out var item))
        {
            if (item.CreatedAt >= minimum) values.Add(item.Value);
        }
        return values;
    }
}
