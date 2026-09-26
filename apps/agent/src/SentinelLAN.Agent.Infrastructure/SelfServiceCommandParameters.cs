using System.Text.Json;
using System.Text.Json.Serialization;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed record ApprovedAppParameters(Guid RequestId, string PackageUrl, string Sha256,
    string PublisherThumbprint, DateTimeOffset ApprovalExpiresAt);
public sealed record MaintenanceParameters(Guid RequestId, DateTimeOffset MaintenanceExpiresAt, int PauseMinutes);

public static class SelfServiceCommandParameters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static bool TryReadApp(RemoteCommand command, DateTimeOffset now, out ApprovedAppParameters? parameters)
    {
        parameters = Read<ApprovedAppParameters>(command.Parameter);
        return parameters is not null && parameters.RequestId != Guid.Empty &&
            parameters.Sha256?.Length == 64 && parameters.Sha256.All(Uri.IsHexDigit) &&
            parameters.PublisherThumbprint?.Length == 40 && parameters.PublisherThumbprint.All(Uri.IsHexDigit) &&
            parameters.ApprovalExpiresAt > now && parameters.ApprovalExpiresAt <= command.IssuedAt.AddMinutes(30) &&
            IsMsiUri(parameters.PackageUrl, out _);
    }

    public static bool TryReadMaintenance(RemoteCommand command, DateTimeOffset now, out MaintenanceParameters? parameters)
    {
        parameters = Read<MaintenanceParameters>(command.Parameter);
        return parameters is not null && parameters.RequestId != Guid.Empty && parameters.PauseMinutes == 15 &&
            parameters.MaintenanceExpiresAt > now && parameters.MaintenanceExpiresAt <= command.IssuedAt.AddMinutes(30);
    }

    public static bool IsMsiUri(string? value, out Uri? uri)
    {
        uri = null;
        return value?.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && !uri.IsLoopback &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
            uri.AbsolutePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
    }

    private static T? Read<T>(string? parameter) where T : class
    {
        if (string.IsNullOrWhiteSpace(parameter) || parameter.Length > 4096) return null;
        try { return JsonSerializer.Deserialize<T>(parameter, JsonOptions); }
        catch (JsonException) { return null; }
    }
}
