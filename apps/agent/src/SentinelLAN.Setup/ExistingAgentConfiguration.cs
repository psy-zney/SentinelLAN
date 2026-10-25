using System.Text.Json;
using SentinelLAN.Enrollment;

namespace SentinelLAN.Setup;

internal static class ExistingAgentConfiguration
{
    public static string ResolveServer(string settingsJson, string? companyServerUrl)
    {
        string server;
        try
        {
            using var settings = JsonDocument.Parse(settingsJson);
            if (!settings.RootElement.TryGetProperty("SENTINELLAN_API_URL", out var value) || value.ValueKind != JsonValueKind.String)
                throw new FormatException();
            server = EnrollmentConnectionCode.NormalizeServerUrl(value.GetString() ?? "");
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException)
        {
            throw new InvalidOperationException("Cấu hình máy đã đăng ký chưa hợp lệ. Nhờ IT kiểm tra, sau đó bấm Thử lại kết nối.");
        }
        if (!string.IsNullOrWhiteSpace(companyServerUrl) &&
            !string.Equals(server, EnrollmentConnectionCode.NormalizeServerUrl(companyServerUrl), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Máy đã đăng ký với máy chủ khác bộ cài này. Nhờ IT cung cấp đúng bộ cài của công ty.");
        return server;
    }
}
