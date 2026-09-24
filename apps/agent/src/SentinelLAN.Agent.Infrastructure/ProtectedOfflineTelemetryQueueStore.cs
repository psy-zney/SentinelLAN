using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class ProtectedOfflineTelemetryQueueStore(string path, string? nonWindowsKey = null) : IOfflineQueueStore<QueuedTelemetry>
{
    private const int MaxFileBytes = 256 * 1024;
    private static readonly byte[] WindowsEntropy = Encoding.UTF8.GetBytes("SentinelLAN offline telemetry queue v1");
    private static readonly byte[] LinuxMagic = [0x53, 0x4c, 0x51, 0x31];
    private static readonly byte[] LinuxPurpose = Encoding.UTF8.GetBytes("SentinelLAN offline telemetry queue v1");
    private readonly byte[]? _linuxKey = DeriveKey(nonWindowsKey);

    public IReadOnlyList<OfflineQueueEntry<QueuedTelemetry>> Load()
    {
        if (!File.Exists(path)) return [];
        var protectedBytes = File.ReadAllBytes(path);
        if (protectedBytes.Length is 0 or > MaxFileBytes)
            throw new InvalidDataException("The protected Agent telemetry queue has an invalid size.");
        var plainBytes = Unprotect(protectedBytes);
        var document = JsonSerializer.Deserialize<QueueDocument>(plainBytes)
            ?? throw new InvalidDataException("The protected Agent telemetry queue is empty or invalid.");
        if (document.Version != 1 || document.Entries is null)
            throw new InvalidDataException("The protected Agent telemetry queue format is unsupported.");
        if (document.Entries.Count > 50)
            throw new InvalidDataException("The protected Agent telemetry queue exceeds its entry limit.");
        return document.Entries;
    }

    public void Save(IReadOnlyList<OfflineQueueEntry<QueuedTelemetry>> entries)
    {
        if (entries.Count > 50) throw new InvalidDataException("The Agent telemetry queue exceeds its entry limit.");
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(new QueueDocument(1, entries));
        var protectedBytes = Protect(plainBytes);
        if (protectedBytes.Length > MaxFileBytes)
            throw new InvalidDataException("The protected Agent telemetry queue exceeds its storage limit.");

        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The Agent data directory is not configured.");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var temporaryPath = Path.Combine(directory, $".telemetry-{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporaryPath, options))
            {
                stream.Write(protectedBytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private byte[] Protect(byte[] plaintext)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Protect(plaintext, WindowsEntropy);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_linuxKey!, tag.Length);
        aes.Encrypt(nonce, plaintext, cipher, tag, LinuxPurpose);
        return [.. LinuxMagic, .. nonce, .. tag, .. cipher];
    }

    private byte[] Unprotect(byte[] protectedBytes)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Unprotect(protectedBytes, WindowsEntropy);
        if (protectedBytes.Length < LinuxMagic.Length + 12 + 16 || !protectedBytes.AsSpan(0, LinuxMagic.Length).SequenceEqual(LinuxMagic))
            throw new CryptographicException("The protected Agent telemetry queue format is invalid.");
        var nonceOffset = LinuxMagic.Length;
        var tagOffset = nonceOffset + 12;
        var cipherOffset = tagOffset + 16;
        var plaintext = new byte[protectedBytes.Length - cipherOffset];
        using var aes = new AesGcm(_linuxKey!, 16);
        aes.Decrypt(protectedBytes.AsSpan(nonceOffset, 12), protectedBytes.AsSpan(cipherOffset), protectedBytes.AsSpan(tagOffset, 16), plaintext, LinuxPurpose);
        return plaintext;
    }

    private static byte[]? DeriveKey(string? secret)
    {
        if (OperatingSystem.IsWindows()) return null;
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("SENTINELLAN_AGENT_STORE_KEY must be a unique secret of at least 32 characters on non-Windows hosts.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(LinuxPurpose);
    }

    private sealed record QueueDocument(int Version, IReadOnlyList<OfflineQueueEntry<QueuedTelemetry>> Entries);
}
