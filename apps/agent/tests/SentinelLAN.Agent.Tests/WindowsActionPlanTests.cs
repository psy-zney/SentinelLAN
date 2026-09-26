using System.Diagnostics;

namespace SentinelLAN.Agent.Tests;

public sealed class WindowsActionPlanTests
{
    [Theory]
    [InlineData("Isolation", 0, "RECOVERY_VERIFIED")]
    [InlineData("RecoveryFailure", 0, "FAILED_RECOVERY_RETAINED")]
    [InlineData("RegistrationFailure", 1, "REGISTRATION_DENIED")]
    [InlineData("RuleVerificationFailure", 1, "OWN_RULES_REMOVED")]
    public async Task FirewallPlanRequiresPriorRecoveryAndPreservesRecoveryWhenRemovalFails(string scenario, int expectedExit, string evidence)
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SentinelLAN.slnx"))) directory = directory.Parent;
        var repo = directory?.FullName ?? throw new DirectoryNotFoundException("Repository test script was not found");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(repo, "scripts/test-windows-action-plans.ps1"), "-Scenario", scenario })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == expectedExit, $"Unexpected exit {process.ExitCode}: {output} {error}");
        Assert.Contains(evidence, output);
        if (scenario == "RegistrationFailure") Assert.DoesNotContain("CREATE_RULE", output);
        else Assert.True(output.IndexOf("RECOVERY_REGISTERED", StringComparison.Ordinal) < output.IndexOf("CREATE_RULE", StringComparison.Ordinal));
    }
}
