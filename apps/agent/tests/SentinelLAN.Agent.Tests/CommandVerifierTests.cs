using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class CommandVerifierTests
{
    private const string StoreKey = "unit-test-only-agent-store-key-32-bytes-min";

    [Fact]
    public async Task ProtectedNonceCacheRejectsReplayAcrossProcessRestart()
    {
        var path = Environment.GetEnvironmentVariable("SENTINELLAN_NONCE_PROBE_PATH");
        if (Environment.GetEnvironmentVariable("SENTINELLAN_NONCE_RESTART_PROBE") == "1")
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
            var nonce = Environment.GetEnvironmentVariable("SENTINELLAN_NONCE_PROBE_VALUE")!;
            var store = new ProtectedCommandNonceStore(path!, StoreKey);
            var command = Command("SimulateLock") with { Nonce = nonce };
            Assert.False(new CommandVerifier(store).TryAccept(command, command.DeviceId, DateTimeOffset.UtcNow, _ => true, out _));
            return;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"sentinellan-nonce-restart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var storePath = Path.Combine(tempDirectory, "command-nonces.dat");
        var nonceValue = Guid.NewGuid().ToString("N");
        try
        {
            var now = DateTimeOffset.UtcNow;
            Assert.True(new ProtectedCommandNonceStore(storePath, StoreKey).TryAdd(nonceValue, now.AddMinutes(10), now));

            var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = FindRepositoryRoot(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.ArgumentList.Add("test");
            process.StartInfo.ArgumentList.Add(Path.Combine(FindRepositoryRoot(), "apps/agent/tests/SentinelLAN.Agent.Tests/SentinelLAN.Agent.Tests.csproj"));
            process.StartInfo.ArgumentList.Add("--no-build");
            process.StartInfo.ArgumentList.Add("--no-restore");
            process.StartInfo.ArgumentList.Add("--configuration");
            process.StartInfo.ArgumentList.Add(new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Development");
            process.StartInfo.ArgumentList.Add("--filter");
            process.StartInfo.ArgumentList.Add("FullyQualifiedName~ProtectedNonceCacheRejectsReplayAcrossProcessRestart");
            process.StartInfo.ArgumentList.Add("--verbosity");
            process.StartInfo.ArgumentList.Add("quiet");
            process.StartInfo.Environment["SENTINELLAN_NONCE_RESTART_PROBE"] = "1";
            process.StartInfo.Environment["SENTINELLAN_NONCE_PROBE_PATH"] = storePath;
            process.StartInfo.Environment["SENTINELLAN_NONCE_PROBE_VALUE"] = nonceValue;
            Assert.True(process.Start(), "Could not start the child test process.");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            Assert.True(process.ExitCode == 0, $"Child process rejected replay test failed. stdout: {output}\nstderr: {error}");
        }
        finally
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProtectedNonceCacheFailsClosedWhenCiphertextIsCorrupt()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"sentinellan-nonce-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var path = Path.Combine(tempDirectory, "command-nonces.dat");
        try
        {
            var now = DateTimeOffset.UtcNow;
            Assert.True(new ProtectedCommandNonceStore(path, StoreKey).TryAdd(Guid.NewGuid().ToString("N"), now.AddMinutes(5), now));
            var ciphertext = File.ReadAllBytes(path);
            ciphertext[^1] ^= 0x80;
            File.WriteAllBytes(path, ciphertext);
            Assert.ThrowsAny<Exception>(() => new ProtectedCommandNonceStore(path, StoreKey));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SentinelLAN.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate SentinelLAN.slnx from the test output directory.");
    }

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
