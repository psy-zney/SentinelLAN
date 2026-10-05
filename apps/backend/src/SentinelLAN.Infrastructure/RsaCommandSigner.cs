using System.Security.Cryptography;
using System.Text.Json;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class RsaCommandSigner : ICommandSigner, IDisposable
{
    private readonly RSA key;
    private readonly string keyId;
    private readonly object gate = new();

    public RsaCommandSigner(string privateKeyPem, string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64 ||
            keyId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Command key ID must contain 1-64 ASCII letters, digits or hyphens.", nameof(keyId));
        key = RSA.Create();
        try
        {
            key.ImportFromPem(privateKeyPem);
            if (key.KeySize < 3072) throw new ArgumentException("Command signing requires RSA of at least 3072 bits.");
            _ = key.ExportParameters(true); // A public-only key must fail startup.
        }
        catch { key.Dispose(); throw; }
        this.keyId = keyId;
    }

    public string Sign(DeviceCommand command)
    {
        lock (gate)
            return $"R2.{keyId}.{Convert.ToBase64String(key.SignData(Payload(command), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))}";
    }

    public bool Verify(DeviceCommand command)
    {
        var parts = command.Signature.Split('.');
        if (parts.Length != 3 || parts[0] != "R2" || parts[1] != keyId || parts[2].Length > 2048) return false;
        try
        {
            var signature = Convert.FromBase64String(parts[2]);
            lock (gate) return key.VerifyData(Payload(command), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception error) when (error is FormatException or CryptographicException) { return false; }
    }

    private byte[] Payload(DeviceCommand command) => JsonSerializer.SerializeToUtf8Bytes(new object?[]
    {
        2, keyId, command.Id, command.OrganizationId, command.DeviceId, command.IssuedByUserId,
        command.Type, command.Reason, command.IssuedAt.ToUnixTimeMilliseconds(),
        command.ExpiresAt.ToUnixTimeMilliseconds(), command.Nonce, command.Parameter
    });

    public void Dispose() => key.Dispose();
}
