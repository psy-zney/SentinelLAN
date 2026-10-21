using System.Security.Cryptography;

namespace SentinelLAN.Infrastructure;

public static class AgentRequestKey
{
    public static byte[] Create(SensitiveDataCipher cipher, Guid organizationId, Guid deviceId, out string encoded)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        try { encoded = Convert.ToBase64String(key); return cipher.EncryptBytes(key, Purpose(organizationId, deviceId)); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public static byte[] Read(SensitiveDataCipher cipher, Guid organizationId, Guid deviceId, byte[] envelope) =>
        cipher.DecryptBytes(envelope, Purpose(organizationId, deviceId));
    private static string Purpose(Guid organizationId, Guid deviceId) => $"agent.request-key:{organizationId:D}:{deviceId:D}";
}
