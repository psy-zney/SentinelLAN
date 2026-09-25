using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class DevelopmentIdentityStore(string path) : IDeviceIdentityStore
{
    public async Task<DeviceIdentity?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DeviceIdentity>(stream, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, identity, cancellationToken: cancellationToken);
    }
}

public sealed class ProtectedDeviceIdentityStore : IDeviceIdentityStore
{
    private static readonly byte[] Entropy = [0x53, 0x65, 0x6e, 0x74, 0x69, 0x6e, 0x65, 0x6c, 0x4c, 0x41, 0x4e, 0x5f, 0x50, 0x72, 0x6f, 0x74];
    private readonly string path;
    private readonly byte[]? nonWindowsKey;

    public ProtectedDeviceIdentityStore(string path, string? nonWindowsKey = null)
    {
        this.path = path;
        if (!OperatingSystem.IsWindows())
        {
            if (string.IsNullOrWhiteSpace(nonWindowsKey) || nonWindowsKey.Length < 32)
                throw new InvalidOperationException("SENTINELLAN_AGENT_STORE_KEY must be a unique secret of at least 32 characters on non-Windows hosts.");
            this.nonWindowsKey = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(nonWindowsKey));
        }
    }

    public async Task<DeviceIdentity?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var plainBytes = Unprotect(protectedBytes);
        return JsonSerializer.Deserialize<DeviceIdentity>(plainBytes);
    }

    public async Task SaveAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        ProtectedStoreDirectory.Ensure(directory);
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(identity);
        var protectedBytes = Protect(plainBytes);
        var temporaryPath = Path.Combine(directory, $".identity-{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(temporaryPath, options))
            {
                await stream.WriteAsync(protectedBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private byte[] Protect(byte[] plainData)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDpapi.Protect(plainData, Entropy);
        }
        return NonWindowsIdentityCipher.Protect(plainData, nonWindowsKey!);
    }

    private byte[] Unprotect(byte[] protectedData)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDpapi.Unprotect(protectedData, Entropy);
        }
        return NonWindowsIdentityCipher.Unprotect(protectedData, nonWindowsKey!);
    }
}

internal static class WindowsDpapi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        [MarshalAs(UnmanagedType.LPWStr)] string szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Protect(byte[] data, byte[] entropy)
    {
        var inBlob = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        var entropyBlob = new DATA_BLOB { cbData = entropy.Length, pbData = Marshal.AllocHGlobal(entropy.Length) };
        var outBlob = new DATA_BLOB();
        try
        {
            Marshal.Copy(data, 0, inBlob.pbData, data.Length);
            Marshal.Copy(entropy, 0, entropyBlob.pbData, entropy.Length);
            if (!CryptProtectData(ref inBlob, "SentinelLAN Device Identity", ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            var result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
            if (entropyBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entropyBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }

    public static byte[] Unprotect(byte[] data, byte[] entropy)
    {
        var inBlob = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        var entropyBlob = new DATA_BLOB { cbData = entropy.Length, pbData = Marshal.AllocHGlobal(entropy.Length) };
        var outBlob = new DATA_BLOB();
        try
        {
            Marshal.Copy(data, 0, inBlob.pbData, data.Length);
            Marshal.Copy(entropy, 0, entropyBlob.pbData, entropy.Length);
            if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            var result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
            if (entropyBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entropyBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }
}

internal static class NonWindowsIdentityCipher
{
    private static readonly byte[] Magic = [0x53, 0x4c, 0x32, 0x00];

    public static byte[] Protect(byte[] data, byte[] key)
    {
        var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new System.Security.Cryptography.AesGcm(key, tag.Length);
        aes.Encrypt(nonce, data, cipher, tag);
        return [.. Magic, .. nonce, .. tag, .. cipher];
    }

    public static byte[] Unprotect(byte[] data, byte[] key)
    {
        if (data.Length < Magic.Length + 12 + 16 || !data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new System.Security.Cryptography.CryptographicException("Unsupported identity format; re-enroll the device.");
        var plain = new byte[data.Length - Magic.Length - 12 - 16];
        using var aes = new System.Security.Cryptography.AesGcm(key, 16);
        aes.Decrypt(data.AsSpan(Magic.Length, 12), data.AsSpan(Magic.Length + 12 + 16),
            data.AsSpan(Magic.Length + 12, 16), plain);
        return plain;
    }
}


public sealed class AgentApi(HttpClient httpClient, string deviceName) : IAgentApi
{
    public async Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/v1/agent/enroll", new { token, deviceName, osVersion = RuntimeInformation.OSDescription, agentVersion = "0.1.0" }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceIdentity>(cancellationToken))!;
    }

    public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken) =>
        SendHeartbeatAsync(identity, telemetry, null, cancellationToken);

    public async Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/agent/heartbeat", identity);
        request.Content = JsonContent.Create(new { idempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString("N"), telemetry.CpuPercent, telemetry.RamPercent, telemetry.DiskPercent, telemetry.OsVersion, telemetry.AgentVersion });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/agent/commands/poll", identity);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteCommand>(cancellationToken);
    }

    public async Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/agent/commands/{commandId}/result", identity);
        request.Content = JsonContent.Create(new { result.Succeeded, result.Message });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path, DeviceIdentity identity)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-SentinelLAN-Device-Id", identity.DeviceId.ToString());
        request.Headers.Add("X-SentinelLAN-Device-Secret", identity.DeviceSecret);
        return request;
    }
}
