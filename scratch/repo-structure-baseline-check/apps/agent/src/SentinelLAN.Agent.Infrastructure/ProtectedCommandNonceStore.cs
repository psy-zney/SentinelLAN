using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

/// <summary>Persists the Agent's recently accepted command nonces using purpose-bound protection.</summary>
public sealed class ProtectedCommandNonceStore : ICommandNonceStore
{
    private const int Capacity = 8192;
    private const int MaxFileBytes = 2 * 1024 * 1024;
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("SentinelLAN command nonce cache v1");
    private static readonly byte[] Magic = [0x53, 0x4c, 0x4e, 0x31];
    private readonly object _gate = new();
    private readonly string _path;
    private readonly byte[]? _nonWindowsKey;
    private Dictionary<string, DateTimeOffset> _nonces;

    public ProtectedCommandNonceStore(string path, string? nonWindowsKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _nonWindowsKey = DeriveKey(nonWindowsKey);
        _nonces = Load(DateTimeOffset.UtcNow);
    }

    public bool TryAdd(string nonce, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(nonce) || nonce.Length > 256 || expiresAt <= now || expiresAt - now > TimeSpan.FromMinutes(15))
            return false;

        lock (_gate)
        {
            var previous = _nonces;
            var next = new Dictionary<string, DateTimeOffset>(previous, StringComparer.Ordinal);
            foreach (var expired in next.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                next.Remove(expired);
            var pruned = next.Count != previous.Count;
            if (next.ContainsKey(nonce))
            {
                if (pruned) { Persist(next); _nonces = next; }
                return false;
            }
            if (next.Count >= Capacity)
            {
                if (pruned) { Persist(next); _nonces = next; }
                return false;
            }
            next.Add(nonce, expiresAt);
            Persist(next);
            _nonces = next;
            return true;
        }
    }

    private Dictionary<string, DateTimeOffset> Load(DateTimeOffset now)
    {
        if (!File.Exists(_path)) return new(StringComparer.Ordinal);
        var protectedBytes = File.ReadAllBytes(_path);
        if (protectedBytes.Length is 0 or > MaxFileBytes)
            throw new InvalidDataException("The protected Agent command nonce cache has an invalid size.");

        var plaintext = Unprotect(protectedBytes);
        var document = JsonSerializer.Deserialize<NonceDocument>(plaintext)
            ?? throw new InvalidDataException("The protected Agent command nonce cache is empty or invalid.");
        if (document.Version != 1 || document.Entries is null || document.Entries.Count > Capacity)
            throw new InvalidDataException("The protected Agent command nonce cache format or size is invalid.");

        var entries = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var entry in document.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Nonce) || entry.Nonce.Length > 256 || entry.AcceptedAt > now ||
                entry.ExpiresAt <= entry.AcceptedAt || entry.ExpiresAt - entry.AcceptedAt > TimeSpan.FromMinutes(15) ||
                entry.ExpiresAt > now.AddMinutes(15))
                throw new InvalidDataException("The protected Agent command nonce cache contains an invalid entry.");
            if (entry.ExpiresAt > now && !entries.TryAdd(entry.Nonce, entry.ExpiresAt))
                throw new InvalidDataException("The protected Agent command nonce cache contains duplicate entries.");
        }

        // Rewrite a pruned cache at startup so expired replay material is not retained on disk.
        if (entries.Count != document.Entries.Count) Persist(entries);
        return entries;
    }

    private void Persist(Dictionary<string, DateTimeOffset> entries)
    {
        var acceptedAt = DateTimeOffset.UtcNow;
        var document = new NonceDocument(1, entries.Select(pair => new NonceEntry(pair.Key, acceptedAt, pair.Value)).ToArray());
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document);
        var protectedBytes = Protect(plaintext);
        if (protectedBytes.Length > MaxFileBytes)
            throw new InvalidDataException("The protected Agent command nonce cache exceeds its storage limit.");

        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("The Agent data directory is not configured.");
        ProtectedStoreDirectory.Ensure(directory);
        var temporaryPath = Path.Combine(directory, $".command-nonces-{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.WriteThrough };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporaryPath, options))
            {
                stream.Write(protectedBytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private byte[] Protect(byte[] plaintext)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Protect(plaintext, Purpose);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_nonWindowsKey!, tag.Length);
        aes.Encrypt(nonce, plaintext, cipher, tag, Purpose);
        return [.. Magic, .. nonce, .. tag, .. cipher];
    }

    private byte[] Unprotect(byte[] protectedBytes)
    {
        if (OperatingSystem.IsWindows()) return WindowsDpapi.Unprotect(protectedBytes, Purpose);
        if (protectedBytes.Length < Magic.Length + 12 + 16 || !protectedBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new CryptographicException("The protected Agent command nonce cache format is invalid.");
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

    private sealed record NonceDocument(int Version, IReadOnlyList<NonceEntry> Entries);
    private sealed record NonceEntry(string Nonce, DateTimeOffset AcceptedAt, DateTimeOffset ExpiresAt);
}
