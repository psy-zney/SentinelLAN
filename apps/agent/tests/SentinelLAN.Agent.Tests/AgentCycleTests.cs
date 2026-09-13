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
        Assert.Equal(1, collector.Samples);
        Assert.Equal(api.HeartbeatKeys[0], api.HeartbeatKeys[1]);
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
        var offlineQueue = new ResilientOfflineQueue<TelemetrySnapshot>(10);
        var cycle = new AgentCycle(new DeviceIdentity(DeviceId, "test"), collector, api, new CommandVerifier(), new Signatures(), TimeProvider.System, offlineQueue);

        // First run: fails and buffers into offlineQueue
        await Assert.ThrowsAsync<HttpRequestException>(() => cycle.RunAsync(default));
        Assert.Equal(1, offlineQueue.Count);

        // Second run: succeeds and flushes buffered snapshot
        api.FailHeartbeat = false;
        await cycle.RunAsync(default);

        // Both the new heartbeat and the flushed offline snapshot are delivered
        Assert.True(api.HeartbeatKeys.Count >= 2);
        Assert.Equal(0, offlineQueue.Count);
    }

    [Fact]
    public async Task ProtectedDeviceIdentityStoreRoundtripSucceeds()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sentinellan_test_{Guid.NewGuid():N}.dat");
        try
        {
            var store = new SentinelLAN.Agent.Infrastructure.ProtectedDeviceIdentityStore(tempFile);
            var id = new DeviceIdentity(Guid.NewGuid(), "super-secret-key-12345");
            await store.SaveAsync(id, default);

            var loaded = await store.LoadAsync(default);
            Assert.NotNull(loaded);
            Assert.Equal(id.DeviceId, loaded.DeviceId);
            Assert.Equal(id.DeviceSecret, loaded.DeviceSecret);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
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
