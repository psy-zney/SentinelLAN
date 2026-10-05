using System.Diagnostics;

namespace SentinelLAN.Agent.Tests;

public sealed class WindowsSetupScriptTests
{
    [Theory]
    [InlineData("Connected")]
    [InlineData("TokenUsed")]
    [InlineData("TokenExpired")]
    [InlineData("ConnectionFailed")]
    public async Task SetupRemovesTemporaryTokenAndPreservesItConfigurationForEveryOutcome(string scenario)
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SentinelLAN.slnx"))) directory = directory.Parent;
        var repo = directory?.FullName ?? throw new DirectoryNotFoundException("Repository test script was not found.");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(repo, "scripts/test-agent-setup.ps1"), "-Scenario", scenario })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.StandardInput.WriteLineAsync(new string('A', 64));
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, await error);
        var receipt = await output;
        Assert.Contains($"SETUP_STATE:{scenario}", receipt);
        Assert.Contains("TOKEN_REMOVED_AND_SETTINGS_PRESERVED", receipt);
    }
}
