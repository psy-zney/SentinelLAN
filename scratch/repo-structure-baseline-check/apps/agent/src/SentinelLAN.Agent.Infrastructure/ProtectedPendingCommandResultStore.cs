using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

/// <summary>Stores at most one short-lived command receipt using platform-bound encryption.</summary>
public sealed class ProtectedPendingCommandResultStore(string path, string? nonWindowsKey = null) : IPendingCommandResultStore
{
    private const int MaxFileBytes = 16 * 1024;
    private const int MaxMessageLength = 2048;
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("SentinelLAN pending command result v1");
    private static readonly byte[] Magic = [0x53, 0x4c, 0x52, 0x31];
    private readonly object _gate = new();
    private readonly byte[]? _nonWindowsKey = DeriveKey(nonWindowsKey);

    public PendingCommandResult? Load()
    {
        lock (_gate)
        {
            if (!File.Exists(path)) return null;
            var protectedBytes = File.ReadAllBytes(path);
            if (protectedBytes.Length is 0 or > MaxFileBytes)
                throw new InvalidDataException("The protected Agent command result has an invalid size.");

            var document = JsonSerializer.Deserialize<ResultDocument>(Unprotect(protectedBytes))
                ?? throw new InvalidDataException("The protected Agent command result is empty or invalid.");
            if (document.Version != 1 || document.CommandId == Guid.Empty || document.Result is null ||
                document.Result.Message is null || document.Result.Message.Length > MaxMessageLength)
                throw new InvalidDataException("The protected Agent command result format is invalid.");

            var now = DateTimeOffset.UtcNow;
            if (document.ExpiresAt <= now)
            {
                ClearCore();
                return null;
            }
            if (document.ExpiresAt > now.AddMinutes(15))
                throw new InvalidDataException("The protected Agent command result expiry is invalid.");

            return new PendingCommandResult(document.CommandId, document.ExpiresAt, document.Result);
        }
    }

    public void Save(PendingCommandResult pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (pending.CommandId == Guid.Empty || pending.Result is null || pending.Result.Message is null ||
            pending.Result.Message.Length > MaxMessageLength)
            throw new InvalidDataException("The Agent command result is invalid or exceeds its storage limit.");
        var now = DateTimeOffset.UtcNow;
        if (pending.ExpiresAt <= now || pending.ExpiresAt > now.AddMinutes(15))
            throw new InvalidDataException("The Agent command result expiry is invalid.");

        lock (_gate)
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(new ResultDocument(1, pending.CommandId, pending.ExpiresAt, pending.Result));
            var protectedBytes = Protect(plaintext);
            if (protectedBytes.Length > MaxFileBytes)
                throw new InvalidDataException("The protected Agent command result exceeds its storage limit.");

            var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The Agent data directory is not configured.");
            ProtectedStoreDirectory.Ensure(directory);

            var temporaryPath = Path.Combine(directory, $".command-result-{Guid.NewGuid():N}.tmp");
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.WriteThrough };
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
    }

    public void Clear()
    {
        lock (_gate) ClearCore();
    }

    private void ClearCore()
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private byte[] Protect(byte[] plaintext)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Protect(plaintext, Purpose);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_nonWindowsKey!, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Purpose);
        return [.. Magic, .. nonce, .. tag, .. ciphertext];
    }

    private byte[] Unprotect(byte[] protectedBytes)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Unprotect(protectedBytes, Purpose);
        if (protectedBytes.Length < Magic.Length + 12 + 16 || !protectedBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new CryptographicException("The protected Agent command result format is invalid.");
        var nonceOffset = Magic.Length;
        var tagOffset = nonceOffset + 12;
        var cipherOffset = tagOffset + 16;
        var plaintext = new byte[protectedBytes.Length - cipherOffset];
        using var aes = new AesGcm(_nonWindowsKey!, 16);
        aes.Decrypt(protectedBytes.AsSpan(nonceOffset, 12), protectedBytes.AsSpan(cipherOffset), protectedBytes.AsSpan(tagOffset, 16), plaintext, Purpose);
        return plaintext;
    }

    private static byte[]? DeriveKey(string? secret)
    {
        if (OperatingSystem.IsWindows()) return null;
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("SENTINELLAN_AGENT_STORE_KEY must be a unique secret of at least 32 characters on non-Windows hosts.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Purpose);
    }

    private sealed record ResultDocument(int Version, Guid CommandId, DateTimeOffset ExpiresAt, ExecutionResult Result);
}
