using System.Security.Cryptography;
using System.Text;

namespace SentinelLAN.Infrastructure;

public sealed class SensitiveDataCipher
{
    private static readonly byte[] Magic = [0x53, 0x4c, 0x4e, 0x00, 0x01];
    private const string Prefix = "sln:v1:";
    private readonly byte[] key;
    public string KeyId { get; }
    internal bool AllowLegacyReading { get; set; }

    public SensitiveDataCipher(string base64Key)
    {
        try { key = Convert.FromBase64String(base64Key); }
        catch (FormatException) { throw new InvalidOperationException("SENTINELLAN_DATA_ENCRYPTION_KEY must be a Base64-encoded 32-byte key."); }
        if (key.Length != 32) throw new InvalidOperationException("SENTINELLAN_DATA_ENCRYPTION_KEY must contain exactly 32 random bytes.");
        KeyId = Convert.ToHexString(SHA256.HashData(key));
    }

    public byte[] EncryptBytes(byte[] plaintext, string purpose)
    {
        var result = new byte[Magic.Length + 12 + 16 + plaintext.Length];
        Magic.CopyTo(result, 0);
        var nonce = result.AsSpan(Magic.Length, 12);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, result.AsSpan(Magic.Length + 28), result.AsSpan(Magic.Length + 12, 16), Encoding.UTF8.GetBytes(purpose));
        return result;
    }

    public byte[] DecryptBytes(byte[] ciphertext, string purpose)
    {
        if (!ciphertext.AsSpan().StartsWith(Magic))
        {
            if (AllowLegacyReading) return ciphertext;
            throw new CryptographicException("An authenticated data envelope is required.");
        }
        if (ciphertext.Length < Magic.Length + 28) throw new CryptographicException("Invalid encrypted data envelope.");
        var result = new byte[ciphertext.Length - Magic.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(ciphertext.AsSpan(Magic.Length, 12), ciphertext.AsSpan(Magic.Length + 28), ciphertext.AsSpan(Magic.Length + 12, 16), result, Encoding.UTF8.GetBytes(purpose));
        return result;
    }

    public string EncryptText(string plaintext, string purpose) => Prefix + Convert.ToBase64String(EncryptBytes(Encoding.UTF8.GetBytes(plaintext), purpose));
    public string DecryptText(string ciphertext, string purpose)
    {
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (AllowLegacyReading) return ciphertext;
            throw new CryptographicException("An authenticated text envelope is required.");
        }
        var envelope = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        if (!envelope.AsSpan().StartsWith(Magic)) throw new CryptographicException("Invalid text envelope.");
        return Encoding.UTF8.GetString(DecryptBytes(envelope, purpose));
    }
}
