using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SentinelLAN.Application;

public static class AgentRequestSignature
{
    public const string TimestampHeader = "X-SentinelLAN-Timestamp";
    public const string NonceHeader = "X-SentinelLAN-Nonce";
    public const string SignatureHeader = "X-SentinelLAN-Signature";
    public const int MaximumBodyBytes = 256 * 1024;
    public static byte[] Sign(ReadOnlySpan<byte> key, Guid deviceId, string method, string target, long timestamp, Guid nonce, ReadOnlySpan<byte> bodyHash) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(string.Join('\n', "SentinelLAN-Agent-Request-1", deviceId.ToString("D"),
            method.ToUpperInvariant(), target, timestamp.ToString(CultureInfo.InvariantCulture), nonce.ToString("N"), Convert.ToHexStringLower(bodyHash))));
}
