using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

/// <summary>Interactive users redeem OTP approvals without reading device credentials.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsMaintenanceBridge
{
    public const string PipeName = "SentinelLAN.Maintenance.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record Request(Guid RequestId, string Code, bool Confirmed);

    public static async Task RunServerAsync(IDeviceIdentityStore identityStore, IAgentApi api, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-2"), PipeAccessRights.FullControl, AccessControlType.Deny));
            foreach (var sid in new[] { "S-1-5-18", "S-1-5-19", "S-1-5-32-544" })
                security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid), PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-4"),
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
            using var pipe = NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 4096, 4096, security);
            await pipe.WaitForConnectionAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId)) throw new Win32Exception(Marshal.GetLastWin32Error());
                using var client = Process.GetProcessById(checked((int)processId));
                if (client.SessionId == 0 || client.SessionId != WTSGetActiveConsoleSessionId())
                    throw new UnauthorizedAccessException("Maintenance client must be in the active console session.");
                var request = await ReadAsync(pipe, timeout.Token);
                if (!request.Confirmed || request.RequestId == Guid.Empty || request.Code?.Length != 8 ||
                    request.Code.Any(character => character is < '0' or > '9'))
                {
                    await WriteAsync(pipe, new ExecutionResult(false, "Nhập mã yêu cầu, mã IT gồm 8 chữ số và xác nhận thao tác."), timeout.Token);
                    continue;
                }
                var identity = await identityStore.LoadAsync(timeout.Token);
                if (identity is null)
                {
                    await WriteAsync(pipe, new ExecutionResult(false, "Máy chưa được đăng ký với IT. Agent vẫn đang hoạt động."), timeout.Token);
                    continue;
                }
                await api.RedeemMaintenanceAsync(identity, request.RequestId, request.Code, timeout.Token);
                await WriteAsync(pipe, new ExecutionResult(true, "Mã đã được xác nhận. Đang chờ lệnh có chữ ký từ IT; thao tác chưa hoàn tất."), timeout.Token);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is
                IOException or JsonException or UnauthorizedAccessException or ArgumentException or Win32Exception or
                HttpRequestException or OperationCanceledException or NotSupportedException)
            {
                // Do not return backend error bodies, device secrets, OTPs or request contents.
                try { await WriteAsync(pipe, new ExecutionResult(false, "Chưa xác nhận được mã: mã sai, đã dùng, hết hạn hoặc mất kết nối. Agent vẫn hoạt động; hãy liên hệ IT."), cancellationToken); }
                catch (IOException) { }
            }
        }
    }

    public static async Task ShowDialogAsync(CancellationToken cancellationToken)
    {
        if (Process.GetCurrentProcess().SessionId == 0)
            throw new InvalidOperationException("Maintenance popup requires the logged-in employee's interactive desktop.");
        var script = Path.Combine(AppContext.BaseDirectory, "maintenance-popup.ps1");
        if (!File.Exists(script)) throw new FileNotFoundException("Maintenance desktop helper is not installed. Ask IT to repair the SentinelLAN MSI.");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-STA", "-File", script }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Maintenance popup could not start.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new IOException("Maintenance desktop popup could not complete; Agent remains active.");
    }

    private static async Task<Request> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > 1024) throw new IOException("Invalid maintenance request length.");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellationToken);
        return JsonSerializer.Deserialize<Request>(data, JsonOptions) ?? throw new IOException("Empty maintenance request.");
    }

    private static async Task WriteAsync(Stream stream, ExecutionResult result, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}
