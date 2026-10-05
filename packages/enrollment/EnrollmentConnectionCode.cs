using System.Text.Json;

namespace SentinelLAN.Enrollment;

/// <summary>Bootstrap data, not proof of authorization. The server consumes the token.</summary>
public sealed record EnrollmentConnectionCode(string ServerUrl, string Token, DateTimeOffset ExpiresAt)
{
    private const string Prefix = "SL1.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Encode()
    {
        Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
        return Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static EnrollmentConnectionCode Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = value.Trim();
        if (value.Length > 4096 || !value.StartsWith(Prefix, StringComparison.Ordinal))
            throw new FormatException("Mã kết nối không hợp lệ hoặc phiên bản chưa được hỗ trợ.");
        try
        {
            var payload = value[Prefix.Length..];
            if (payload.Length == 0 || payload.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
                throw new FormatException();
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var code = JsonSerializer.Deserialize<EnrollmentConnectionCode>(Convert.FromBase64String(base64), JsonOptions)
                ?? throw new FormatException();
            code.Validate();
            return code;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            throw new FormatException("Mã kết nối không hợp lệ. Hãy sao chép đầy đủ mã do IT cấp.");
        }
    }

    public static string NormalizeServerUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || (uri.Scheme != "https" && !uri.IsLoopback) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new FormatException("Địa chỉ máy chủ phải là HTTPS tin cậy, không chứa đường dẫn hoặc thông tin đăng nhập.");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private void Validate()
    {
        _ = NormalizeServerUrl(ServerUrl);
        if (string.IsNullOrEmpty(Token) || Token.Length != 64 || Token.Any(c => !char.IsAsciiHexDigit(c)) || ExpiresAt == default)
            throw new FormatException("Invalid enrollment connection code.");
    }
}
