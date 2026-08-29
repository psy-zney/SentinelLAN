using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AccessTokenService(string signingKey) : IAccessTokenService
{
    private const string Issuer = "SentinelLAN";
    private const string Audience = "SentinelLAN.Web";
    private readonly byte[] key = Encoding.UTF8.GetBytes(signingKey);

    public string Create(User user, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = Issuer,
            aud = Audience,
            sub = user.Id,
            org = user.OrganizationId,
            role = user.Role,
            iat = now.ToUnixTimeSeconds(),
            exp = expiresAt.ToUnixTimeSeconds(),
            jti = Guid.NewGuid()
        }));
        var signature = Sign($"{header}.{payload}");
        return $"{header}.{payload}.{signature}";
    }

    public ActorContext? Validate(string token, DateTimeOffset now)
    {
        if (token.Length > 4096) return null;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            var expected = Sign($"{parts[0]}.{parts[1]}");
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2]))) return null;

            using var header = JsonDocument.Parse(Decode(parts[0]));
            if (header.RootElement.GetProperty("alg").GetString() != "HS256" || header.RootElement.GetProperty("typ").GetString() != "JWT") return null;

            using var payload = JsonDocument.Parse(Decode(parts[1]));
            var root = payload.RootElement;
            if (root.GetProperty("iss").GetString() != Issuer || root.GetProperty("aud").GetString() != Audience) return null;
            if (root.GetProperty("exp").GetInt64() <= now.ToUnixTimeSeconds()) return null;
            if (root.GetProperty("iat").GetInt64() > now.AddMinutes(1).ToUnixTimeSeconds()) return null;

            var role = root.GetProperty("role").GetString();
            if (role is not (Roles.Admin or Roles.Technician or Roles.Employee)) return null;
            return new ActorContext(root.GetProperty("sub").GetGuid(), root.GetProperty("org").GetGuid(), role);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    private string Sign(string value) => Encode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
}
