namespace SentinelLAN.Application;

public static class DeviceRequestValidation
{
    public static bool IsValid(EnrollRequest request) =>
        HasText(request.Token, 256) && HasText(request.DeviceName, 200) &&
        HasText(request.OsVersion, 300) && HasText(request.AgentVersion, 100);

    public static bool IsValid(HeartbeatRequest request) =>
        HasText(request.IdempotencyKey, 128) &&
        IsPercent(request.CpuPercent) && IsPercent(request.RamPercent) && IsPercent(request.DiskPercent) &&
        HasText(request.OsVersion, 300) && HasText(request.AgentVersion, 100);

    private static bool HasText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;

    private static bool IsPercent(double value) => double.IsFinite(value) && value is >= 0 and <= 100;
}
