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
