using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class HmacCommandVerifier(string? key) : ICommandSignatureVerifier
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(key);
    public bool Verify(RemoteCommand command)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(command.Signature)) return false;
        var data = JsonSerializer.Serialize(new object[] { command.Id, command.OrganizationId, command.DeviceId, command.IssuedByUserId, command.Type, command.Reason, command.IssuedAt.ToUnixTimeMilliseconds(), command.ExpiresAt.ToUnixTimeMilliseconds(), command.Nonce });
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(command.Signature)); }
        catch (FormatException) { return false; }
    }
}
