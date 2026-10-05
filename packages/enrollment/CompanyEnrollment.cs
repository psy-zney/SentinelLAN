namespace SentinelLAN.Enrollment;

public static class CompanyEnrollment
{
    public static EnrollmentConnectionCode Resolve(string token, string companyServerUrl)
    {
        var server = EnrollmentConnectionCode.NormalizeServerUrl(companyServerUrl);
        token = token.Trim();
        if (token.Length != 64 || token.Any(c => !char.IsAsciiHexDigit(c)))
            throw new FormatException("Mã kết nối không hợp lệ. Hãy dán đầy đủ mã do IT cấp cho công ty.");
        // Expiry is authoritative on the server; opaque tokens carry no metadata.
        return new EnrollmentConnectionCode(server, token, DateTimeOffset.MaxValue);
    }
}
