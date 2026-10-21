using System.Net;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class LinuxAdapterTests
{
    private static readonly Guid Device = Guid.NewGuid();
    private static RemoteCommand Command(string type, string? parameter = null) => new(Guid.NewGuid(), Device, type, "Authorized lab", "nonce", "signature", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(2), Parameter: parameter);
    [Theory]
    [InlineData("LockWorkstation")]
    [InlineData("IsolateNetwork")]
    public async Task SensitiveActionsRequireMatchingLocalLabAuthorization(string type)
    {
        var platform = new Platform(); var executor = new LinuxCommandExecutor(new(), new("https://sentinel.example.test"), platform);
        Assert.False((await executor.ExecuteAsync(Command(type), default)).Succeeded); Assert.Empty(platform.Calls);
    }
    [Fact]
    public async Task IsolationNeverAppliesRulesUntilRollbackTimerIsVerified()
    {
        var platform = new Platform { TimerActive = false }; var executor = new LinuxCommandExecutor(new(LabExecution: true, AuthorizedDeviceId: Device, IsolationServerIpv4: "192.0.2.10"), new("https://sentinel.example.test"), platform);
        Assert.False((await executor.ExecuteAsync(Command("IsolateNetwork", "30"), default)).Succeeded);
        Assert.DoesNotContain(platform.Calls, x => x == "nft --file -");
        platform.Calls.Clear(); platform.TimerActive = true;
        Assert.True((await executor.ExecuteAsync(Command("IsolateNetwork", "30"), default)).Succeeded);
        Assert.True(platform.Calls.FindIndex(x => x.StartsWith("systemctl is-active", StringComparison.Ordinal)) < platform.Calls.FindIndex(x => x == "nft --file -"));
        Assert.DoesNotContain("flush ruleset", platform.Rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ip daddr 192.0.2.53 udp dport 53", platform.Rules);
    }
    [Theory]
    [InlineData("nginx; reboot")]
    [InlineData("ssh")]
    public async Task RestartRejectsUnapprovedNamesWithoutStartingAProcess(string service)
    {
        var platform = new Platform(); var executor = new LinuxCommandExecutor(new(AllowServiceRestart: true), new("https://sentinel.example.test"), platform);
        Assert.False((await executor.ExecuteAsync(Command("RestartService", service), default)).Succeeded); Assert.Empty(platform.Calls);
    }
    private sealed class Platform : ILinuxCommandPlatform
    {
        public bool IsLinux => true;
        public string NftExecutable => "/usr/sbin/nft";
        public IReadOnlyList<IPAddress> DnsServers => [IPAddress.Parse("192.0.2.53")];
        public List<string> Calls { get; } = [];
        public string Rules { get; private set; } = "";
        public bool TimerActive { get; set; } = true;
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(new[] { IPAddress.Parse("192.0.2.10") });
        public Task<LinuxProcessResult> RunAsync(string program, IReadOnlyList<string> arguments, string? input, CancellationToken ct)
        {
            Calls.Add(program + " " + string.Join(' ', arguments)); if (input is not null) Rules = input;
            return Task.FromResult(program == "id" ? new LinuxProcessResult(0, "0") : program == "systemctl" ? new(TimerActive ? 0 : 1, TimerActive ? "active" : "inactive") : new(0, "ok"));
        }
    }
}
