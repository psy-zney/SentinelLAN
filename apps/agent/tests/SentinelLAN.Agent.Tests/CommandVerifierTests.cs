using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

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
        var lockResult = SafeCommandExecutor.Execute(Command("SimulateLock"), allowRealExecution: true);
        var isolationResult = SafeCommandExecutor.Execute(Command("SimulateNetworkIsolation"), allowRealExecution: true);

        Assert.True(lockResult.Succeeded);
        Assert.Contains("simulated", lockResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unchanged", lockResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(isolationResult.Succeeded);
        Assert.Contains("simulated", isolationResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unchanged", isolationResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LabEnvironmentFlagCannotEnableRealSimulationActions()
    {
        var previousLabFlag = Environment.GetEnvironmentVariable("SENTINELLAN_LAB_EXECUTION");
        var previousCommandsFlag = Environment.GetEnvironmentVariable("SENTINELLAN_ALLOW_REAL_COMMANDS");
        try
        {
            Environment.SetEnvironmentVariable("SENTINELLAN_LAB_EXECUTION", "true");
            Environment.SetEnvironmentVariable("SENTINELLAN_ALLOW_REAL_COMMANDS", "true");

            var lockResult = SafeCommandExecutor.Execute(Command("SimulateLock"));
            var isolationResult = SafeCommandExecutor.Execute(Command("SimulateNetworkIsolation"));

            Assert.Contains("simulated", lockResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("simulated", isolationResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("successfully", lockResult.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTINELLAN_LAB_EXECUTION", previousLabFlag);
            Environment.SetEnvironmentVariable("SENTINELLAN_ALLOW_REAL_COMMANDS", previousCommandsFlag);
        }
    }

    [Fact]
    public void RestartServiceOnlyAcceptsAllowListedNamesAndAlwaysSimulates()
    {
        foreach (var service in new[] { "docker", " nginx ", "CADDY" })
        {
            var result = SafeCommandExecutor.Execute(Command("RestartService", service), allowRealExecution: true);
            Assert.True(result.Succeeded);
            Assert.Contains("simulated", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("unchanged", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var service in new[] { null, "", "   ", "ssh", "docker; touch /tmp/sentinel", "nginx --force" })
        {
            var result = SafeCommandExecutor.Execute(Command("RestartService", service));
            Assert.False(result.Succeeded);
        }
    }

    [Fact]
    public void UserVisibleCommandMessagesDoNotClaimUnperformedActions()
    {
        var notification = SafeCommandExecutor.Execute(Command("ShowNotification"));
        var telemetry = SafeCommandExecutor.Execute(Command("CollectTelemetryNow"));
        var policy = SafeCommandExecutor.Execute(Command("RefreshPolicy"));

        Assert.Contains("nothing was displayed", notification.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no immediate collection", telemetry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no policy was applied", policy.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HmacBindsRestartServiceParameter()
    {
        const string key = "test-signing-key";
        var command = Command("RestartService", "docker") with { Signature = string.Empty };
        var signed = command with { Signature = Sign(command, key) };
        var verifier = new HmacCommandVerifier(key);

        Assert.True(verifier.Verify(signed));
        Assert.False(verifier.Verify(signed with { Parameter = "nginx" }));
    }

    private static RemoteCommand Command(string type, string? parameter = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, "Demo", Guid.NewGuid().ToString("N"), "sig", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), Parameter: parameter);

    private static string Sign(RemoteCommand command, string key)
    {
        var data = JsonSerializer.Serialize(new object?[]
        {
            command.Id, command.OrganizationId, command.DeviceId, command.IssuedByUserId,
            command.Type, command.Reason, command.IssuedAt.ToUnixTimeMilliseconds(),
            command.ExpiresAt.ToUnixTimeMilliseconds(), command.Nonce, command.Parameter
        });
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data)));
    }
}
