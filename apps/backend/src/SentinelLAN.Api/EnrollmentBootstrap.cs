using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Enrollment;

namespace SentinelLAN.Api;

public sealed record EnrollmentBootstrapSettings(string ServerUrl, string PublicKey, string KeyId, string ArtifactUrl, string Sha256, string? Publisher);
public sealed record EnrollmentBootstrapPackage(string ConnectionCode, DateTimeOffset ExpiresAt, string FileName, string Script);
public static class EnrollmentBootstrap
{
    public static EnrollmentBootstrapSettings Read(IConfiguration config, string platform)
    {
        var server = EnrollmentConnectionCode.NormalizeServerUrl(config["SENTINELLAN_AGENT_PUBLIC_URL"] ?? "");
        using var rsa = RSA.Create(); rsa.ImportFromPem(config["SENTINELLAN_COMMAND_PRIVATE_KEY_PEM"] ?? "");
        if (rsa.KeySize < 3072) throw new InvalidOperationException("RSA-PSS 3072-bit is required.");
        var prefix = platform == "Windows" ? "SENTINELLAN_BOOTSTRAP_WINDOWS_" : "SENTINELLAN_BOOTSTRAP_LINUX_";
        var url = config[prefix + "URL"]; var hash = config[prefix + "SHA256"]; var publisher = config[prefix + "PUBLISHER"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            hash?.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)) || platform == "Windows" && (publisher?.Length != 40 || publisher.Any(c => !char.IsAsciiHexDigit(c))))
            throw new InvalidOperationException("Configure the trusted release URL, SHA-256 and Windows Authenticode publisher.");
        var keyId = config["SENTINELLAN_COMMAND_KEY_ID"] ?? "";
        if (keyId.Length is < 1 or > 64 || keyId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new InvalidOperationException("Invalid command key ID.");
        return new(server, Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()), keyId, uri.AbsoluteUri, hash, publisher);
    }
    public static EnrollmentBootstrapPackage Create(EnrollmentBootstrapSettings settings, string token, DateTimeOffset expiry, string platform, int index)
    {
        var connection = new EnrollmentConnectionCode(settings.ServerUrl, token, expiry).Encode();
        var config = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { settings.ServerUrl, settings.PublicKey, settings.KeyId, settings.ArtifactUrl, settings.Sha256, settings.Publisher, Token = token, ExpiresAt = expiry }));
        var name = $"sentinellan-enroll-{index:00}.{(platform == "Windows" ? "ps1" : "sh")}";
        return new(connection, expiry, name, platform == "Windows" ? Windows(config) : Linux(config));
    }
    private static string Resource(string name)
    {
        using var stream = typeof(EnrollmentBootstrap).Assembly.GetManifestResourceStream($"SentinelLAN.Bootstrap.{name}") ?? throw new InvalidOperationException("Missing bootstrap resource");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    private static string Windows(string config) => "# Contains one 15-minute enrollment secret. Keep private and delete after use.\n$ErrorActionPreference = 'Stop'\n$payload = '" + config + "'\n$configure = '" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Resource("configure-windows-agent.ps1"))) + "'\n" + Resource("bootstrap-windows.ps1");
    private static string Linux(string config) => "#!/usr/bin/env bash\n# Contains one 15-minute enrollment secret. Keep private and delete after use.\nset -euo pipefail\nset +x\numask 077\npayload='" + config + "'\n" + Resource("bootstrap-linux.sh");
}
