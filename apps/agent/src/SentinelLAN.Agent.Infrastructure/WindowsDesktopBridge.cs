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

[SupportedOSPlatform("windows")]
public static class WindowsDesktopBridge
{
    private const string PipeName = "SentinelLAN.Desktop.v1";
    private sealed record Request(RemoteCommand Command, AgentPolicySnapshot? Policy = null);
    private sealed record Response(Guid CommandId, ExecutionResult Result);

    public static async Task<ExecutionResult> SendAsync(RemoteCommand command, CancellationToken cancellationToken,
        AgentPolicySnapshot? policy = null)
    {
        var target = WTSGetActiveConsoleSessionId();
        if (target == uint.MaxValue) return new(false, "No active console session is available; remote desktop sessions are not targeted");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { "S-1-5-18", "S-1-5-19", "S-1-5-32-544" })
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid), PipeAccessRights.FullControl, AccessControlType.Allow));
        // Interactive clients can exchange messages, but cannot create another server instance.
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-4"),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        using var pipe = NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 4096, 4096, security);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token);
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId)) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var client = Process.GetProcessById(checked((int)processId));
            if (client.SessionId != target) return new(false, "Desktop companion is not in the active console session");
            await WriteAsync(pipe, new Request(command, policy), timeout.Token);
            var response = await ReadAsync<Response>(pipe, timeout.Token);
            if (response.CommandId != command.Id || response.Result is null || string.IsNullOrWhiteSpace(response.Result.Message) || response.Result.Message.Length > 1800)
                return new(false, "Desktop response did not match the verified command or was invalid");
            // Independently verify the session instead of trusting a lock acknowledgement alone.
            if (command.Type == "LockWorkstation" && response.Result.Succeeded && !WindowsDesktopActions.IsLocked((int)target))
                return new(false, "Desktop companion replied, but the console session was not locked");
            return response.Result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Desktop companion did not complete within 12 seconds; start SentinelLAN.Agent.exe --desktop-companion in the logged-in user's session");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return new(false, "Desktop companion response or session was invalid; OS execution was not confirmed");
        }
    }

    public static async Task RunCompanionAsync(AgentExecutionOptions options, CancellationToken cancellationToken)
    {
        if (Process.GetCurrentProcess().SessionId == 0) throw new InvalidOperationException("Desktop companion must run in an interactive user session");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Identification);
                await pipe.ConnectAsync(3000, cancellationToken);
                VerifyServer(pipe);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var request = await ReadAsync<Request>(pipe, timeout.Token);
                var command = request.Command;
                var result = command.ExpiresAt <= DateTimeOffset.UtcNow
                    ? new ExecutionResult(false, "Desktop command expired")
                    : command.Type switch
                    {
                        "ShowNotification" => WindowsDesktopActions.Notify(command.Parameter ?? command.Reason),
                        "LockWorkstation" when options.LabExecution && options.AuthorizedDeviceId != Guid.Empty && options.AuthorizedDeviceId == command.DeviceId
                            => await WindowsDesktopActions.LockAsync(timeout.Token),
                        "RefreshPolicy" when options.AllowPolicyChanges && request.Policy is not null
                            => WindowsDesktopActions.ApplyIdleTimeout(request.Policy.IdleTimeoutMinutes),
                        _ => new(false, "Desktop action is unsupported or not locally authorized")
                    };
                await WriteAsync(pipe, new Response(command.Id, result), timeout.Token);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is
                TimeoutException or IOException or UnauthorizedAccessException or Win32Exception or OperationCanceledException or JsonException)
            {
                // Never log the envelope, notification body, secrets, or policy contents.
                Console.Error.WriteLine($"Desktop companion unavailable: {exception.GetType().Name}");
            }
            await Task.Delay(500, cancellationToken);
        }
    }

    private static void VerifyServer(NamedPipeClientStream pipe)
    {
        // A standard interactive user need not be allowed to open the LocalService process token.
        // Read the kernel pipe object's owner instead. An unprivileged user cannot assign a trusted owner.
        var owner = pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        if (owner?.Value is not ("S-1-5-18" or "S-1-5-19" or "S-1-5-32-544"))
            throw new UnauthorizedAccessException("Desktop pipe is not owned by LocalService, SYSTEM or Administrators");
    }

    private static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value);
        if (data.Length > 16384) throw new IOException("Desktop packet exceeds its size limit");
        var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, data.Length);
        await stream.WriteAsync(length, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > 16384) throw new IOException("Invalid desktop packet length");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellationToken);
        return JsonSerializer.Deserialize<T>(data) ?? throw new IOException("Empty desktop packet");
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}
