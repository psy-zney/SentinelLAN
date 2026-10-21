using System.Diagnostics;
using Microsoft.Win32;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class WindowsPolicyApplier(AgentExecutionOptions options) : IAgentPolicyApplier
{
    public async Task<ExecutionResult> ApplyAsync(AgentPolicySnapshot policy, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "Policy enforcement requires the Windows adapter");
        if (!options.AllowPolicyChanges) return new(false, "Policy enforcement is disabled; configure SENTINELLAN_ALLOW_POLICY_CHANGES=true and required registry permissions");
        if (policy.Id == Guid.Empty || policy.IdleTimeoutMinutes is < 1 or > 1440 || policy.UsbMode is not ("Blocked" or "ReadOnly" or "FullAccess"))
            return new(false, "Server policy contains unsupported settings; OS unchanged");
        cancellationToken.ThrowIfCancellationRequested();
        // Open both writable keys before making any changes, to catch the usual permission failure first.
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\USBSTOR", writable: true);
        using var storage = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies", writable: true);
        if (usb is null || storage is null) return new(false, "Windows USB storage registry keys are unavailable");
        var idle = Process.GetCurrentProcess().SessionId == 0
            ? await WindowsDesktopBridge.SendAsync(new RemoteCommand(Guid.NewGuid(), Guid.Empty, "RefreshPolicy",
                "Apply assigned policy", string.Empty, string.Empty, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(15)), cancellationToken, policy)
            : WindowsDesktopActions.ApplyIdleTimeout(policy.IdleTimeoutMinutes);
        if (!idle.Succeeded) return idle;
        usb.SetValue("Start", policy.UsbMode == "Blocked" ? 4 : 3, RegistryValueKind.DWord);
        storage.SetValue("WriteProtect", policy.UsbMode == "ReadOnly" ? 1 : 0, RegistryValueKind.DWord);
        if (!Equals(usb.GetValue("Start"), policy.UsbMode == "Blocked" ? 4 : 3) ||
            !Equals(storage.GetValue("WriteProtect"), policy.UsbMode == "ReadOnly" ? 1 : 0))
            return new(false, "Idle setting applied, but USB registry settings could not be verified");
        return new(true, $"Policy {policy.Id} applied: {idle.Message}. USB {policy.UsbMode} registry settings verified; reconnect storage or restart Windows to enforce driver changes. Existing mounted storage was not forcibly detached.");
    }
}
