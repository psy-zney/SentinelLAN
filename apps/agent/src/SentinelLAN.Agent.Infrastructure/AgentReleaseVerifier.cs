using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SentinelLAN.Agent.Infrastructure;

public static class AgentReleaseVerifier
{
    public const long MaxPackageBytes = 256L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed record Envelope(string Payload, string Signature);

    public static void ValidateSettings(AgentUpdateSettings settings)
    {
        _ = HttpsUrl(settings.ManifestUrl);
        if (HttpsUrl(settings.CompanyServerUrl).AbsolutePath != "/")
            throw new InvalidDataException("Company update scope must be an HTTPS origin.");
        if (settings.PublisherThumbprint is not { Length: 40 } || !settings.PublisherThumbprint.All(char.IsAsciiHexDigit) ||
            string.IsNullOrWhiteSpace(settings.PublicKeyPem) || settings.PublicKeyPem.Length > 8192 ||
            !settings.PublicKeyPem.Trim().StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) ||
            !settings.PublicKeyPem.Trim().EndsWith("-----END PUBLIC KEY-----", StringComparison.Ordinal))
            throw new InvalidDataException("Update trust settings are invalid.");
    }

    public static AgentRelease Verify(byte[] envelopeBytes, AgentUpdateSettings settings, DateTimeOffset now)
    {
        ValidateSettings(settings);
        try
        {
            if (envelopeBytes.Length > 64 * 1024) throw new InvalidDataException("Release manifest is too large.");
            var envelope = JsonSerializer.Deserialize<Envelope>(envelopeBytes, JsonOptions)
                ?? throw new InvalidDataException("Missing signed release manifest.");
            var payload = Convert.FromBase64String(envelope.Payload);
            var signature = Convert.FromBase64String(envelope.Signature);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(settings.PublicKeyPem);
            if (rsa.KeySize < 3072 || !rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new InvalidDataException("Release signature is not trusted.");
            var release = JsonSerializer.Deserialize<AgentRelease>(payload, JsonOptions)
                ?? throw new InvalidDataException("Missing release details.");
            var package = HttpsUrl(release.PackageUrl);
            var manifest = HttpsUrl(settings.ManifestUrl);
            if (release.SchemaVersion != 1 || release.ReleaseId == Guid.Empty ||
                !TryVersion(release.Version, out _) ||
                release.SizeBytes is < 8 or > MaxPackageBytes ||
                release.Sha256 is not { Length: 64 } || !release.Sha256.All(char.IsAsciiHexDigit) ||
                release.PublishedAt > now.AddMinutes(5) || release.ExpiresAt <= now ||
                release.ExpiresAt <= release.PublishedAt || release.ExpiresAt - release.PublishedAt > TimeSpan.FromDays(7) ||
                !string.Equals(package.GetLeftPart(UriPartial.Authority), manifest.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) ||
                !package.AbsolutePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                HttpsUrl(release.CompanyServerUrl) != HttpsUrl(settings.CompanyServerUrl) ||
                string.IsNullOrWhiteSpace(release.Reason) || release.Reason.Length > 256 || release.Reason.Any(char.IsControl))
                throw new InvalidDataException("Release scope, expiry, version or package metadata is invalid.");
            return release;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or CryptographicException or ArgumentException)
        {
            throw new InvalidDataException("Release manifest could not be verified.", exception);
        }
    }

    public static bool TryVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (value is null || value.Length > 16 || value.Split('.').Length != 3 ||
            value.Any(character => !char.IsAsciiDigit(character) && character != '.') ||
            !Version.TryParse(value, out var parsed) || parsed.Major > 255 || parsed.Minor > 255 || parsed.Build > 65535)
            return false;
        version = parsed;
        return true;
    }

    private static Uri HttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.IsLoopback ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidDataException("Updates require trusted HTTPS URLs without credentials, query or fragment.");
        return uri;
    }
}
