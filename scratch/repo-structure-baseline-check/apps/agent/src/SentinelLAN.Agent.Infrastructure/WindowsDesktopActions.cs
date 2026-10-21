using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

[SupportedOSPlatform("windows")]
public static class WindowsDesktopActions
{
    public static async Task<ExecutionResult> LockAsync(CancellationToken cancellationToken)
    {
        var sessionId = Process.GetCurrentProcess().SessionId;
        if (sessionId == 0) return new(false, "Session 0 cannot lock an interactive desktop; start the desktop companion in the user's session");
        if (IsLocked(sessionId)) return new(true, $"Windows session {sessionId} is already locked; state verified through WTS");
        if (!LockWorkStation()) throw new Win32Exception(Marshal.GetLastWin32Error());
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(100, cancellationToken);
            if (IsLocked(sessionId)) return new(true, $"Windows session {sessionId} locked; state verified through WTS");
        }
        return new(false, "Windows accepted the lock request, but a locked session was not observed within four seconds");
    }

    public static ExecutionResult Notify(string message)
    {
        const string title = "SentinelLAN — authorized administrator notification";
        var sessionId = Process.GetCurrentProcess().SessionId;
        if (sessionId == 0) return new(false, "No interactive desktop; start the desktop companion");
        if (string.IsNullOrWhiteSpace(message) || message.Length > 1000) return new(false, "Notification must contain 1-1000 characters");
        if (!WTSSendMessageW(IntPtr.Zero, sessionId, title, title.Length * 2, message, message.Length * 2,
            0x40, 3, out var response, true)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(true, $"Windows accepted notification for session {sessionId}; WTS response {response} (timeout does not prove the user read it)");
    }

    public static ExecutionResult ApplyIdleTimeout(int minutes)
    {
        if (Process.GetCurrentProcess().SessionId == 0) return new(false, "Idle policy requires the desktop companion in the user's session");
        if (minutes is < 1 or > 1440) return new(false, "Idle timeout must be 1-1440 minutes");
        var screenSaver = Path.Combine(Environment.SystemDirectory, "scrnsave.scr");
        if (!File.Exists(screenSaver)) return new(false, "Windows secure screen saver binary is unavailable; idle policy was not applied");
        var seconds = checked((uint)minutes * 60);
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)
            ?? throw new IOException("User desktop registry key is unavailable");
        desktop.SetValue("ScreenSaveTimeOut", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        desktop.SetValue("ScreenSaveActive", "1");
        desktop.SetValue("ScreenSaverIsSecure", "1");
        desktop.SetValue("SCRNSAVE.EXE", screenSaver);
        if (!SystemParametersInfoW(15, seconds, IntPtr.Zero, 3) || !SystemParametersInfoW(17, 1, IntPtr.Zero, 3) ||
            !SystemParametersInfoW(0x77, 1, IntPtr.Zero, 3))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!SystemParametersInfoW(14, 0, out var actualSeconds, 0) || actualSeconds != seconds ||
            !SystemParametersInfoW(16, 0, out var active, 0) || active == 0 ||
            !SystemParametersInfoW(0x76, 0, out var secure, 0) || secure == 0)
            return new(false, "Idle policy registry was written, but Windows did not confirm the active timeout");
        return new(true, $"Active Windows idle timeout verified: {minutes} minutes; secure screen saver configured for this user");
    }

    public static bool IsLocked(int sessionId)
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, 25, out var buffer, out var bytes))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            // WTSINFOEX: DWORD Level, 8-byte-aligned union; LEVEL1 starts with session ID/state/flags.
            if (bytes < 20 || Marshal.ReadInt32(buffer) != 1 || Marshal.ReadInt32(buffer, 8) != sessionId)
                throw new IOException("Windows returned unsupported session information");
            return Marshal.ReadInt32(buffer, 16) == 0;
        }
        finally { WTSFreeMemory(buffer); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSSendMessageW(IntPtr server, int sessionId, string title, int titleLength,
        string message, int messageLength, int style, int timeout, out int response, [MarshalAs(UnmanagedType.Bool)] bool wait);

    [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytes);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr buffer);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint parameter, IntPtr value, uint flags);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint parameter, out uint value, uint flags);
}
