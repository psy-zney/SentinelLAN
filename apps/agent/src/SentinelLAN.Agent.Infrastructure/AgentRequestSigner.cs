using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public static class AgentRequestSigner
{
    public const int MaximumBodyBytes = 256 * 1024;
    public static bool IsValidKey(string? value)
    {
        Span<byte> bytes = stackalloc byte[32];
        return value?.Length == 44 && Convert.TryFromBase64String(value, bytes, out var count) && count == 32;
    }
    public static IReadOnlyDictionary<string, string> Headers(DeviceIdentity identity, string method, string target,
        ReadOnlySpan<byte> body, TimeProvider? clock = null)
    {
        if (body.Length > MaximumBodyBytes) throw new HttpRequestException("Agent request exceeds its size limit.");
        var key = Convert.FromBase64String(identity.RequestSigningKey ?? throw new InvalidOperationException("Agent request signing has not been provisioned."));
        try
        {
            if (key.Length != 32) throw new InvalidOperationException("Agent request signing key must contain 32 bytes.");
            var timestamp = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var nonce = Guid.NewGuid().ToString("N");
            var canonical = string.Join('\n', "SentinelLAN-Agent-Request-1", identity.DeviceId.ToString("D"), method.ToUpperInvariant(),
                target, timestamp, nonce, Convert.ToHexStringLower(SHA256.HashData(body)));
            return new Dictionary<string, string>
            {
                ["X-SentinelLAN-Device-Id"] = identity.DeviceId.ToString("D"),
                ["X-SentinelLAN-Timestamp"] = timestamp,
                ["X-SentinelLAN-Nonce"] = nonce,
                ["X-SentinelLAN-Signature"] = "S1." + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical)))
            };
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
