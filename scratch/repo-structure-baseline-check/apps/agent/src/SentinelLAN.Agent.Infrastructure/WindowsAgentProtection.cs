using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;

namespace SentinelLAN.Agent.Infrastructure;

[SupportedOSPlatform("windows")]
public static class WindowsAgentProtection
{
    // Standard users receive no terminate/write rights. IT Administrators retain recovery access.
    public const string ProcessSddl = "D:P(A;;0x1fffff;;;SY)(A;;0x1fffff;;;BA)(A;;0x1fffff;;;LS)";
    public const string ServiceSddl = "D:P(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLORC;;;BU)";

    public static void ProtectServiceProcess()
    {
        if (Process.GetCurrentProcess().SessionId != 0)
            throw new InvalidOperationException("Process protection applies only to the installed Windows service.");
        var descriptor = new RawSecurityDescriptor(ProcessSddl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        if (!SetKernelObjectSecurity(Process.GetCurrentProcess().Handle, 4, bytes))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKernelObjectSecurity(IntPtr handle, uint securityInformation, byte[] securityDescriptor);
}
