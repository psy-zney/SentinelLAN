using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class RealCommandExecutionTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();

    [Theory]
    [InlineData("LockWorkstation")]
    [InlineData("IsolateNetwork")]
    public async Task RealSensitiveActionsFailWithoutLocalLabAuthorization(string type)
    {
        var result = await new WindowsCommandExecutor(new(), new Uri("https://sentinel.example.com"))
            .ExecuteAsync(Command(type), default);
        Assert.False(result.Succeeded);
        if (OperatingSystem.IsWindows()) Assert.Contains("LAB_DEVICE_ID", result.Message);
    }

    [Theory]
    [InlineData("LockWorkstation")]
    [InlineData("IsolateNetwork")]
    public async Task LabFlagCannotAuthorizeAnotherDevice(string type)
    {
        var executor = new WindowsCommandExecutor(new(LabExecution: true, AuthorizedDeviceId: Guid.NewGuid()),
            new Uri("https://sentinel.example.com"));
        Assert.False((await executor.ExecuteAsync(Command(type), default)).Succeeded);
    }

    [Theory]
    [InlineData("docker; Stop-Computer")]
    [InlineData("nginx --force")]
    [InlineData("ssh")]
    [InlineData(null)]
    public async Task ServiceNamesCannotBecomeExecutableCode(string? service)
    {
        var result = await new WindowsCommandExecutor(new(AllowServiceRestart: true), new Uri("https://sentinel.example.com"))
            .ExecuteAsync(Command("RestartService") with { Parameter = service }, default);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ImmediateTelemetryCollectsAgainAndOnlySucceedsAfterHttpAcknowledgement()
    {
        var api = new Api { Next = Command("CollectTelemetryNow") };
        var collector = new Collector();
        await Cycle(api, collector).RunAsync(default);
        Assert.Equal(2, collector.Count);
        Assert.Equal([1d, 2d], api.Measurements.Select(value => value.CpuPercent));
        Assert.EndsWith(api.NextId.ToString("N"), api.Keys[1]);
        Assert.True(Assert.Single(api.Results).Succeeded);
    }

    [Fact]
    public async Task LostImmediateTelemetryResponseIsReportedAsUnconfirmed()
    {
        var api = new Api { Next = Command("CollectTelemetryNow"), FailImmediateTelemetry = true };
        await Cycle(api, new Collector()).RunAsync(default);
        Assert.False(Assert.Single(api.Results).Succeeded);
    }

    [Fact]
    public async Task FailedReceiptRetryDoesNotRepeatTheOsAction()
    {
        var api = new Api { Next = Command("LockWorkstation"), FailReceipt = true };
        var executor = new Executor();
        var cycle = Cycle(api, new Collector(), executor);
        await Assert.ThrowsAsync<HttpRequestException>(() => cycle.RunAsync(default));
        await cycle.RunAsync(default);
        Assert.Equal(1, executor.Calls);
        Assert.Equal(2, api.Results.Count);
        Assert.Equal(api.Results[0], api.Results[1]);
        Assert.False(api.Results[0].Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidSignatureOrExpiredCommandCannotReachOsAdapter(bool expired)
    {
        var command = Command("LockWorkstation");
        if (expired) command = command with { IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-2), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var api = new Api { Next = command };
        var executor = new Executor();
        var cycle = new AgentCycle(new(DeviceId, "test"), new Collector(), api, new(), new Signatures(expired), TimeProvider.System, executor: executor);
        Assert.NotNull(await cycle.RunAsync(default));
        Assert.Equal(0, executor.Calls);
        Assert.Empty(api.Results);
    }

    [Fact]
    public async Task AssignedPolicyIsFetchedBeforeApplyingAndMissingAssignmentFails()
    {
        var api = new Api { Next = Command("RefreshPolicy"), Policy = new(Guid.NewGuid(), 15, "ReadOnly") };
        var applier = new PolicyApplier();
        await Cycle(api, new Collector(), policy: applier).RunAsync(default);
        Assert.Equal(api.Policy, applier.Applied);
        Assert.True(Assert.Single(api.Results).Succeeded);

        api = new Api { Next = Command("RefreshPolicy") };
        applier = new PolicyApplier();
        await Cycle(api, new Collector(), policy: applier).RunAsync(default);
        Assert.Null(applier.Applied);
        Assert.False(Assert.Single(api.Results).Succeeded);
    }

    [Fact]
    public async Task PolicyChangesFailWhenNotOptedIn()
    {
        var result = await new WindowsPolicyApplier(new()).ApplyAsync(new(Guid.NewGuid(), 15, "Blocked"), default);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void WindowsSessionStateCanBeReadWithoutChangingTheDesktop()
    {
        if (!OperatingSystem.IsWindows()) return;
        var session = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        if (session == 0) return;
        _ = WindowsDesktopActions.IsLocked(session);
    }

    private static RemoteCommand Command(string type) => new(Guid.NewGuid(), DeviceId, type, "Authorized unit test",
        Guid.NewGuid().ToString("N"), "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(2));
    private static AgentCycle Cycle(Api api, Collector collector, ICommandExecutor? executor = null, IAgentPolicyApplier? policy = null) =>
        new(new(DeviceId, "test"), collector, api, new(), new Signatures(true), TimeProvider.System, executor: executor, policyApplier: policy);
    private sealed class Collector : ITelemetryCollector
    {
        public int Count { get; private set; }
        public TelemetrySnapshot Collect() => new(++Count, 20, 30, "test", "test");
    }
    private sealed class Signatures(bool valid) : ICommandSignatureVerifier
    {
        public bool IsConfigured => true;
        public bool Verify(RemoteCommand command) => valid;
    }
    private sealed class Executor : ICommandExecutor
    {
        public int Calls { get; private set; }
        public Task<ExecutionResult> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ExecutionResult(false, "Desktop companion missing"));
        }
    }
    private sealed class PolicyApplier : IAgentPolicyApplier
    {
        public AgentPolicySnapshot? Applied { get; private set; }
        public Task<ExecutionResult> ApplyAsync(AgentPolicySnapshot policy, CancellationToken cancellationToken)
        {
            Applied = policy;
            return Task.FromResult(new ExecutionResult(true, "Test adapter applied policy"));
        }
    }
    private sealed class Api : IAgentApi
    {
        public RemoteCommand? Next { get; set; }
        public Guid NextId { get; private set; }
        public AgentPolicySnapshot? Policy { get; init; }
        public bool FailImmediateTelemetry { get; init; }
        public bool FailReceipt { get; set; }
        public List<TelemetrySnapshot> Measurements { get; } = [];
        public List<string> Keys { get; } = [];
        public List<ExecutionResult> Results { get; } = [];
        public Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, string? idempotencyKey, CancellationToken cancellationToken)
        {
            Measurements.Add(telemetry);
            Keys.Add(idempotencyKey!);
            if (FailImmediateTelemetry && idempotencyKey!.StartsWith("command-", StringComparison.Ordinal)) throw new HttpRequestException("Lost response");
            return Task.CompletedTask;
        }
        public Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken)
        {
            var result = Next;
            Next = null;
            if (result is not null) NextId = result.Id;
            return Task.FromResult(result);
        }
        public Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken)
        {
            Results.Add(result);
            if (FailReceipt) { FailReceipt = false; throw new HttpRequestException("Lost receipt"); }
            return Task.CompletedTask;
        }
        public Task<AgentPolicySnapshot?> GetPolicyAsync(DeviceIdentity identity, CancellationToken cancellationToken) => Task.FromResult(Policy);
    }
}
