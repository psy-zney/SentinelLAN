using System.Security.Cryptography;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class RsaCommandVerifier : ICommandSignatureVerifier
{
    private readonly byte[] publicKey;
    private readonly string keyId;
    public bool IsConfigured => true;

    public RsaCommandVerifier(string publicKeyBase64, string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64 ||
            keyId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Invalid command verification key ID.", nameof(keyId));
        publicKey = Convert.FromBase64String(publicKeyBase64);
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKey, out var read);
        if (read != publicKey.Length || rsa.KeySize < 3072)
            throw new ArgumentException("Verification requires a complete RSA public key of at least 3072 bits.");
        this.keyId = keyId;
    }

    public bool Verify(RemoteCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Signature)) return false;
        var parts = command.Signature.Split('.');
        if (parts.Length != 3 || parts[0] != "R2" || parts[1] != keyId || parts[2].Length > 2048) return false;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            var payload = JsonSerializer.SerializeToUtf8Bytes(new object?[]
            {
                2, keyId, command.Id, command.OrganizationId, command.DeviceId, command.IssuedByUserId,
                command.Type, command.Reason, command.IssuedAt.ToUnixTimeMilliseconds(),
                command.ExpiresAt.ToUnixTimeMilliseconds(), command.Nonce, command.Parameter
            });
            return rsa.VerifyData(payload, Convert.FromBase64String(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception error) when (error is FormatException or CryptographicException) { return false; }
    }
}
