using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class SecretHash
{
    public static string Create(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool Matches(string value, string hash) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Create(value)), Convert.FromHexString(hash));
}

public sealed class HmacCommandSigner(string key) : ICommandSigner
{
    public string Sign(DeviceCommand command)
    {
        var data = JsonSerializer.Serialize(new object[] { command.Id, command.OrganizationId, command.DeviceId, command.IssuedByUserId, command.Type, command.Reason, command.IssuedAt.ToUnixTimeMilliseconds(), command.ExpiresAt.ToUnixTimeMilliseconds(), command.Nonce });
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data)));
    }

    public bool Verify(DeviceCommand command)
    {
        var expected = Sign(command);
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(command.Signature)); }
        catch (FormatException) { return false; }
    }
}
