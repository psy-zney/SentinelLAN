using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Tests;

public sealed class AgentCycleTests
{
    [Fact]
    public async Task HeartbeatRetryPreservesSnapshotAndIdempotencyKey()
    {
        var api = new FakeApi { FailHeartbeat = true };
        var collector = new Collector();
        var cycle = Cycle(api, collector, new Signatures());
        await Assert.ThrowsAsync<HttpRequestException>(() => cycle.RunAsync(default));
        await cycle.RunAsync(default);
        Assert.Equal(2, collector.Samples);
        Assert.Equal(api.HeartbeatKeys[0], api.HeartbeatKeys[1]);
        Assert.Equal(3, api.HeartbeatKeys.Count);
        Assert.False(string.IsNullOrEmpty(api.HeartbeatKeys[0]));
    }

    [Fact]
    public async Task ResultRetrySendsSameReceiptBeforePollingAnotherCommand()
    {
        var api = new FakeApi { FailResult = true, Command = Command() };
        var cycle = Cycle(api, new Collector(), new Signatures());
        await Assert.ThrowsAsync<HttpRequestException>(() => cycle.RunAsync(default));
        Assert.Equal(1, api.Polls);
        await cycle.RunAsync(default);
        Assert.Equal(2, api.Results.Count);
        Assert.Equal(api.Results[0], api.Results[1]);
        Assert.Equal(2, api.Polls);
    }

    [Fact]
    public async Task ProtectedPendingResultSurvivesRestartAndRetriesWithoutPollingOrReexecutingIt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sentinellan_result_{Guid.NewGuid():N}");
        var path = Path.Combine(tempDir, "pending-command-result.dat");
        const string key = "test-agent-store-key-at-least-32-characters";
        var command = Command();
        var api = new FakeApi { FailResult = true, Command = command };
        try
        {
            var firstCycle = new AgentCycle(new DeviceIdentity(DeviceId, "test"), new Collector(), api,
                new CommandVerifier(), new Signatures(), TimeProvider.System,
                pendingResultStore: new SentinelLAN.Agent.Infrastructure.ProtectedPendingCommandResultStore(path, key));

            await Assert.ThrowsAsync<HttpRequestException>(() => firstCycle.RunAsync(default));
            Assert.Equal(1, api.Polls);
            Assert.Single(api.Results);
            Assert.Equal(command.Id, api.Results[0].Id);

            // A new process loads and resends the stored receipt before asking for another command.
            var restartedCycle = new AgentCycle(new DeviceIdentity(DeviceId, "test"), new Collector(), api,
                new CommandVerifier(), new Signatures(), TimeProvider.System,
                pendingResultStore: new SentinelLAN.Agent.Infrastructure.ProtectedPendingCommandResultStore(path, key));
            await restartedCycle.RunAsync(default);

            Assert.Equal(2, api.Results.Count);
            Assert.Equal(api.Results[0], api.Results[1]);
            Assert.Equal(2, api.Polls);
            Assert.Null(new SentinelLAN.Agent.Infrastructure.ProtectedPendingCommandResultStore(path, key).Load());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task MissingKeyKeepsTelemetryActiveWithoutConsumingCommandsAndBadSignatureCannotExecute()
    {
        var api = new FakeApi { Command = Command() };
        await Cycle(api, new Collector(), new Signatures { IsConfigured = false }).RunAsync(default);
        Assert.Single(api.HeartbeatKeys);
        Assert.Equal(0, api.Polls);
        Assert.Empty(api.Results);
        var rejection = await Cycle(api, new Collector(), new Signatures { Valid = false }).RunAsync(default);
        Assert.Equal("Invalid signature", rejection);
        Assert.Empty(api.Results);
    }

    [Fact]
    public void ResilientOfflineQueueBuffersAndDrainsCorrectly()
    {
        var queue = new ResilientOfflineQueue<string>(capacity: 3);
        queue.Enqueue("msg1");
        queue.Enqueue("msg2");
        queue.Enqueue("msg3");
        queue.Enqueue("msg4");

        Assert.Equal(3, queue.Count);
        var drained = queue.Drain(TimeSpan.FromMinutes(5));
        Assert.Equal(3, drained.Count);
        Assert.Equal(["msg2", "msg3", "msg4"], drained);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task OfflineHeartbeatIsBufferedAndFlushedOnNextSuccessfulCycle()
    {
        var api = new FakeApi { FailHeartbeat = true };
        var collector = new Collector();
        var offlineQueue = new ResilientOfflineQueue<QueuedTelemetry>(10);
        var cycle = new AgentCycle(new DeviceIdentity(DeviceId, "test"), collector, api, new CommandVerifier(), new Signatures(), TimeProvider.System, offlineQueue);

        // First run: fails and buffers into offlineQueue
        await Assert.ThrowsAsync<HttpRequestException>(() => cycle.RunAsync(default));
        Assert.Equal(1, offlineQueue.Count);

        // Second run: succeeds and flushes buffered snapshot
        api.FailHeartbeat = false;
        await cycle.RunAsync(default);

        // The failed heartbeat keeps its idempotency key and is sent before the new sample.
        Assert.Equal(3, api.HeartbeatKeys.Count);
        Assert.Equal(api.HeartbeatKeys[0], api.HeartbeatKeys[1]);
        Assert.Equal(0, offlineQueue.Count);
    }

    [Fact]
    public async Task ProtectedDeviceIdentityStoreRoundtripSucceeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sentinellan_test_{Guid.NewGuid():N}");
        var tempFile = Path.Combine(tempDir, "identity.dat");
        try
        {
            var store = new SentinelLAN.Agent.Infrastructure.ProtectedDeviceIdentityStore(tempFile, "test-agent-store-key-at-least-32-characters");
            var id = new DeviceIdentity(Guid.NewGuid(), "super-secret-key-12345");
            await store.SaveAsync(id, default);

            var loaded = await store.LoadAsync(default);
            Assert.NotNull(loaded);
            Assert.Equal(id.DeviceId, loaded.DeviceId);
            Assert.Equal(id.DeviceSecret, loaded.DeviceSecret);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProtectedDeviceIdentityStoreRejectsSharedDirectoryWithoutChangingItsPermissions()
    {
        if (OperatingSystem.IsWindows()) return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"sentinellan_shared_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var sharedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(tempDir, sharedMode);
            var store = new SentinelLAN.Agent.Infrastructure.ProtectedDeviceIdentityStore(
                Path.Combine(tempDir, "identity.dat"), "test-agent-store-key-at-least-32-characters");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.SaveAsync(
                new DeviceIdentity(Guid.NewGuid(), "secret"), default));
            Assert.Equal(sharedMode, File.GetUnixFileMode(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProtectedOfflineTelemetryQueueSurvivesRestartAndPreservesIdempotencyKey()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sentinellan_queue_{Guid.NewGuid():N}");
        var path = Path.Combine(tempDir, "offline-telemetry.dat");
        const string key = "test-agent-store-key-at-least-32-characters";
        try
        {
            var firstQueue = new ResilientOfflineQueue<QueuedTelemetry>(
                50, new SentinelLAN.Agent.Infrastructure.ProtectedOfflineTelemetryQueueStore(path, key));
            var item = new QueuedTelemetry(new TelemetrySnapshot(10, 20, 30, "test-os", "test-agent"), "stable-idempotency-key");
            firstQueue.Enqueue(item);

            var restartedQueue = new ResilientOfflineQueue<QueuedTelemetry>(
                50, new SentinelLAN.Agent.Infrastructure.ProtectedOfflineTelemetryQueueStore(path, key));

            Assert.Equal(1, restartedQueue.Count);
            Assert.True(restartedQueue.TryPeek(TimeSpan.FromHours(1), out var restored));
            Assert.Equal(item, restored);
            Assert.True(restartedQueue.TryDequeue(out _));
            Assert.Empty(new ResilientOfflineQueue<QueuedTelemetry>(
                50, new SentinelLAN.Agent.Infrastructure.ProtectedOfflineTelemetryQueueStore(path, key))
                .Drain(TimeSpan.FromHours(1)));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OfflineQueueRecoveryRemovesExpiredEntriesAndEnforcesCapacity()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sentinellan_queue_{Guid.NewGuid():N}");
        var path = Path.Combine(tempDir, "offline-telemetry.dat");
        const string key = "test-agent-store-key-at-least-32-characters";
        try
        {
            var store = new SentinelLAN.Agent.Infrastructure.ProtectedOfflineTelemetryQueueStore(path, key);
            var old = DateTimeOffset.UtcNow.AddHours(-2);
            var now = DateTimeOffset.UtcNow;
            store.Save([
                new OfflineQueueEntry<QueuedTelemetry>(old, new(new(1, 1, 1, "old", "v"), "expired")),
                new OfflineQueueEntry<QueuedTelemetry>(now, new(new(2, 2, 2, "a", "v"), "key-a")),
                new OfflineQueueEntry<QueuedTelemetry>(now.AddTicks(1), new(new(3, 3, 3, "b", "v"), "key-b")),
                new OfflineQueueEntry<QueuedTelemetry>(now.AddTicks(2), new(new(4, 4, 4, "c", "v"), "key-c"))
            ]);

            var queue = new ResilientOfflineQueue<QueuedTelemetry>(2, store);

            Assert.Equal(2, queue.Count);
            Assert.True(queue.TryPeek(TimeSpan.FromHours(1), out var first));
            Assert.Equal("key-b", first!.IdempotencyKey);
            Assert.Equal(["key-b", "key-c"], queue.Drain(TimeSpan.FromHours(1)).Select(value => value.IdempotencyKey));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private static readonly Guid DeviceId = Guid.NewGuid();
    private static RemoteCommand Command() => new(Guid.NewGuid(), DeviceId, "SimulateLock", "Authorized test", Guid.NewGuid().ToString("N"), "signature", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(2));
    private static AgentCycle Cycle(FakeApi api, Collector collector, Signatures signatures) => new(new DeviceIdentity(DeviceId, "test"), collector, api, new CommandVerifier(), signatures, TimeProvider.System);

    private sealed class Collector : ITelemetryCollector
    {
        public int Samples { get; private set; }
        public TelemetrySnapshot Collect() { ++Samples; return new(10, 20, 30, "Windows", "test"); }
    }

    private sealed class Signatures : ICommandSignatureVerifier
    {
        public bool IsConfigured { get; init; } = true;
        public bool Valid { get; init; } = true;
        public bool Verify(RemoteCommand command) => Valid;
    }

    private sealed class FakeApi : IAgentApi
    {
        public bool FailHeartbeat { get; set; }
        public bool FailResult { get; set; }
        public RemoteCommand? Command { get; set; }
        public int Polls { get; private set; }
        public List<string?> HeartbeatKeys { get; } = [];
        public List<(Guid Id, ExecutionResult Result)> Results { get; } = [];
        public Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken) => throw new InvalidOperationException("Retries must supply a stable key.");
        public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, string? idempotencyKey, CancellationToken cancellationToken)
        {
            HeartbeatKeys.Add(idempotencyKey);
            if (FailHeartbeat) { FailHeartbeat = false; throw new HttpRequestException("Lost response"); }
            return Task.CompletedTask;
        }
        public Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken)
        {
            ++Polls;
            var next = Command;
            Command = null;
            return Task.FromResult(next);
        }
        public Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken)
        {
            Results.Add((commandId, result));
            if (FailResult) { FailResult = false; throw new HttpRequestException("Lost receipt response"); }
            return Task.CompletedTask;
        }
    }
}
