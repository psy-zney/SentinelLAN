using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SentinelLAN.Application;

public static class IncidentIdempotency
{
    public static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= 128 &&
        key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    public static string Fingerprint(Guid deviceId, string title, string? description, string severity)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            DeviceId = deviceId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Severity = severity
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
