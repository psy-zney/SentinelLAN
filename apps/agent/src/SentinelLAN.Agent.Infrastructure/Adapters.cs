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

public sealed class ProtectedDeviceIdentityStore(string path) : IDeviceIdentityStore
{
    private static readonly byte[] Entropy = [0x53, 0x65, 0x6e, 0x74, 0x69, 0x6e, 0x65, 0x6c, 0x4c, 0x41, 0x4e, 0x5f, 0x50, 0x72, 0x6f, 0x74];

    public async Task<DeviceIdentity?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var plainBytes = Unprotect(protectedBytes);
        return JsonSerializer.Deserialize<DeviceIdentity>(plainBytes);
    }

    public async Task SaveAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(identity);
        var protectedBytes = Protect(plainBytes);
        await File.WriteAllBytesAsync(path, protectedBytes, cancellationToken);
    }

    private static byte[] Protect(byte[] plainData)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDpapi.Protect(plainData, Entropy);
        }
        return FallbackCipher.Protect(plainData, Entropy);
    }

    private static byte[] Unprotect(byte[] protectedData)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDpapi.Unprotect(protectedData, Entropy);
        }
        return FallbackCipher.Unprotect(protectedData, Entropy);
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

internal static class FallbackCipher
{
    public static byte[] Protect(byte[] data, byte[] entropy)
    {
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = System.Security.Cryptography.SHA256.HashData(entropy);
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new System.Security.Cryptography.CryptoStream(ms, aes.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    public static byte[] Unprotect(byte[] data, byte[] entropy)
    {
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = System.Security.Cryptography.SHA256.HashData(entropy);
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(data, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var ms = new MemoryStream();
        using (var cs = new System.Security.Cryptography.CryptoStream(new MemoryStream(data, iv.Length, data.Length - iv.Length), aes.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Read))
        {
            cs.CopyTo(ms);
        }
        return ms.ToArray();
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
