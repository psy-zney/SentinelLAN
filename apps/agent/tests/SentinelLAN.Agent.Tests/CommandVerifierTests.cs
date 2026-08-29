using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Tests;

public sealed class CommandVerifierTests
{
    [Fact]
    public void RejectsExpiredAndReplayedCommands()
    {
        var deviceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var verifier = new CommandVerifier();
        var expired = new RemoteCommand(Guid.NewGuid(), deviceId, "SimulateLock", "Demo", "expired", "sig", now.AddMinutes(-3), now.AddMinutes(-1));
        Assert.False(verifier.TryAccept(expired, deviceId, now, _ => true, out _));
        var valid = expired with { Id = Guid.NewGuid(), Nonce = "once", IssuedAt = now, ExpiresAt = now.AddMinutes(1) };
        Assert.True(verifier.TryAccept(valid, deviceId, now, _ => true, out _));
        Assert.False(verifier.TryAccept(valid, deviceId, now, _ => true, out _));
    }

    [Fact]
    public void LockAndIsolationAreSimulationOnly()
    {
        var command = new RemoteCommand(Guid.NewGuid(), Guid.NewGuid(), "SimulateNetworkIsolation", "Demo", "nonce", "sig", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        var result = SafeCommandExecutor.Execute(command);
        Assert.True(result.Succeeded);
        Assert.Contains("simulated", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
