namespace SentinelLAN.Agent.Core;

public record DeviceIdentity(Guid DeviceId, string DeviceSecret);
public record RemoteCommand(Guid Id, Guid DeviceId, string Type, string Reason, string Nonce, string Signature, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, Guid OrganizationId = default, Guid IssuedByUserId = default, string? Parameter = null);
public interface ICommandSignatureVerifier { bool IsConfigured { get; } bool Verify(RemoteCommand command); }
public interface ICommandNonceStore { bool TryAdd(string nonce, DateTimeOffset expiresAt, DateTimeOffset now); }
public sealed class InMemoryCommandNonceStore : ICommandNonceStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _nonces = new(StringComparer.Ordinal);

    public bool TryAdd(string nonce, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        lock (_gate)
        {
            foreach (var expired in _nonces.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                _nonces.Remove(expired);
            if (_nonces.ContainsKey(nonce)) return false;
            _nonces.Add(nonce, expiresAt);
            return true;
        }
    }
}
public record ExecutionResult(bool Succeeded, string Message);
public sealed record PendingCommandResult(Guid CommandId, DateTimeOffset ExpiresAt, ExecutionResult Result);
public interface IPendingCommandResultStore
{
    PendingCommandResult? Load();
    void Save(PendingCommandResult pending);
    void Clear();
}

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
    private readonly ICommandNonceStore _nonceStore;

    public CommandVerifier(ICommandNonceStore? nonceStore = null) => _nonceStore = nonceStore ?? new InMemoryCommandNonceStore();

    public bool TryAccept(RemoteCommand command, Guid expectedDeviceId, DateTimeOffset now, Func<RemoteCommand, bool> verifySignature, out string reason)
    {
        if (command.DeviceId != expectedDeviceId) { reason = "Wrong device"; return false; }
        if (!Allowed.Contains(command.Type)) { reason = "Unsupported type"; return false; }
        if (string.IsNullOrWhiteSpace(command.Nonce) || string.IsNullOrWhiteSpace(command.Signature) || command.ExpiresAt <= command.IssuedAt || command.ExpiresAt - command.IssuedAt > TimeSpan.FromMinutes(15)) { reason = "Invalid command envelope"; return false; }
        if (now < command.IssuedAt.AddMinutes(-1) || now >= command.ExpiresAt) { reason = "Expired or issued in the future"; return false; }
        if (!verifySignature(command)) { reason = "Invalid signature"; return false; }
        if (!_nonceStore.TryAdd(command.Nonce, command.ExpiresAt, now)) { reason = "Replay detected or nonce capacity reached"; return false; }
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


public sealed record OfflineQueueEntry<T>(DateTimeOffset CreatedAt, T Value);

public interface IOfflineQueueStore<T>
{
    IReadOnlyList<OfflineQueueEntry<T>> Load();
    void Save(IReadOnlyList<OfflineQueueEntry<T>> entries);
}

public class ResilientOfflineQueue<T>
{
    private readonly object _gate = new();
    private readonly List<OfflineQueueEntry<T>> _items;
    private readonly IOfflineQueueStore<T>? _store;
    private readonly int _capacity;
    private readonly TimeSpan _maxRetention;

    public ResilientOfflineQueue(int capacity = 50, IOfflineQueueStore<T>? store = null, TimeSpan? maxRetention = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _store = store;
        _maxRetention = maxRetention ?? TimeSpan.FromHours(1);
        if (_maxRetention <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxRetention));
        _items = store?.Load().ToList() ?? [];
        if (Prune(DateTimeOffset.UtcNow - _maxRetention)) _store?.Save(_items);
    }
    public int Count { get { lock (_gate) return _items.Count; } }

    public void Enqueue(T value)
    {
        lock (_gate)
        {
            var previous = _items.ToArray();
            Prune(DateTimeOffset.UtcNow - _maxRetention);
            _items.Add(new OfflineQueueEntry<T>(DateTimeOffset.UtcNow, value));
            while (_items.Count > _capacity) _items.RemoveAt(0);
            try { _store?.Save(_items); }
            catch { _items.Clear(); _items.AddRange(previous); throw; }
        }
    }

    public bool TryPeek(TimeSpan retention, out T? value)
    {
        lock (_gate)
        {
            var previous = _items.ToArray();
            if (Prune(DateTimeOffset.UtcNow - Min(retention, _maxRetention)))
            {
                try { _store?.Save(_items); }
                catch { _items.Clear(); _items.AddRange(previous); throw; }
            }
            if (_items.Count > 0) { value = _items[0].Value; return true; }
            value = default;
            return false;
        }
    }

    public bool TryDequeue(out T? value)
    {
        lock (_gate)
        {
            if (_items.Count == 0) { value = default; return false; }
            var first = _items[0];
            _items.RemoveAt(0);
            try { _store?.Save(_items); }
            catch { _items.Insert(0, first); throw; }
            value = first.Value;
            return true;
        }
    }

    public IReadOnlyList<T> Drain(TimeSpan retention)
    {
        lock (_gate)
        {
            var minimum = DateTimeOffset.UtcNow - Min(retention, _maxRetention);
            var previous = _items.ToArray();
            var values = _items.Where(item => item.CreatedAt >= minimum).Select(item => item.Value).ToArray();
            _items.Clear();
            try { _store?.Save(_items); }
            catch { _items.AddRange(previous); throw; }
            return values;
        }
    }

    private bool Prune(DateTimeOffset minimum)
    {
        var removed = _items.RemoveAll(item => item.CreatedAt < minimum) > 0;
        while (_items.Count > _capacity) { _items.RemoveAt(0); removed = true; }
        return removed;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
}
