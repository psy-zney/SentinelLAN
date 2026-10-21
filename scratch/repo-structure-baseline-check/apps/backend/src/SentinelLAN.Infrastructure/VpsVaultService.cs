using System.Security.Cryptography;
using System.Text;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class VpsVaultService : IVpsVaultService
{
    private readonly byte[] _key;

    public VpsVaultService(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        // Derive standard 256-bit (32 bytes) key using SHA256
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secretKey));
    }

    public string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes
        var cipherBytes = new byte[plainBytes.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combined payload: Nonce (12) + Tag (16) + Ciphertext
        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);
        var combined = Convert.FromBase64String(cipherText);

        const int nonceSize = 12;
        const int tagSize = 16;
        if (combined.Length < nonceSize + tagSize)
            throw new CryptographicException("Ciphertext payload is invalid or corrupted.");

        var nonce = new byte[nonceSize];
        var tag = new byte[tagSize];
        var cipherLength = combined.Length - nonceSize - tagSize;
        var cipherBytes = new byte[cipherLength];
        var plainBytes = new byte[cipherLength];

        Buffer.BlockCopy(combined, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(combined, nonceSize, tag, 0, tagSize);
        Buffer.BlockCopy(combined, nonceSize + tagSize, cipherBytes, 0, cipherLength);

        try
        {
            using var aes = new AesGcm(_key, tagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicException("Ciphertext authentication tag mismatch.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}
