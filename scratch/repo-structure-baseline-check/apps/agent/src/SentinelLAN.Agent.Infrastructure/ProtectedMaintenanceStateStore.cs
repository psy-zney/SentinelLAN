using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

/// <summary>Persists maintenance authorization before any external installer is started.</summary>
public sealed class ProtectedMaintenanceStateStore(string path, string? nonWindowsKey = null) : IAgentMaintenanceStateStore
{
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("SentinelLAN approved maintenance v1");
    private readonly object _gate = new();
    private readonly byte[]? _key = OperatingSystem.IsWindows() ? null : DeriveKey(nonWindowsKey);

    public AgentMaintenanceState? Load(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length is < 28 or > 8192) throw new InvalidDataException("Maintenance state has an invalid size.");
            var state = JsonSerializer.Deserialize<AgentMaintenanceState>(Unprotect(bytes))
                ?? throw new InvalidDataException("Maintenance state is invalid.");
            Validate(state);
            if (state.MaintenanceUntil <= now) return null;
            if (state.MaintenanceUntil > now.AddMinutes(30)) throw new InvalidDataException("Maintenance state exceeds its bounded duration.");
            return state;
        }
    }

    public void Save(AgentMaintenanceState state)
    {
        lock (_gate)
        {
            Validate(state);
            var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Maintenance data directory is required.");
            ProtectedStoreDirectory.EnsurePrivate(directory);
            var temporaryPath = Path.Combine(directory, $".maintenance-{Guid.NewGuid():N}.tmp");
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.WriteThrough };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var stream = new FileStream(temporaryPath, options))
                {
                    stream.Write(Protect(JsonSerializer.SerializeToUtf8Bytes(state)));
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }
    }

    private static void Validate(AgentMaintenanceState state)
    {
        if (state.CommandId == Guid.Empty || state.RequestId == Guid.Empty || state.Action is not ("PauseAgent" or "UninstallAgent") ||
            state.MaintenanceUntil > state.AuthorizationExpiresAt.AddMinutes(15) ||
            (state.Authorization is not null && (state.Authorization.Id != state.CommandId || state.Authorization.Type != state.Action)))
            throw new InvalidDataException("Maintenance authorization is invalid.");
    }

    private byte[] Protect(byte[] data)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Protect(data, Purpose);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key!, 16);
        aes.Encrypt(nonce, data, cipher, tag, Purpose);
        return [.. nonce, .. tag, .. cipher];
    }

    private byte[] Unprotect(byte[] data)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Unprotect(data, Purpose);
        var plain = new byte[data.Length - 28];
        using var aes = new AesGcm(_key!, 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain, Purpose);
        return plain;
    }

    private static byte[] DeriveKey(string? secret)
    {
        if (secret?.Length is not >= 32) throw new InvalidOperationException("A private Agent store key is required on non-Windows hosts.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Purpose);
    }
}
