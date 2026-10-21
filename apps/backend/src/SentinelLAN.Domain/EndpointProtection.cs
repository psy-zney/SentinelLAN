namespace SentinelLAN.Domain;

public static class EndpointProtection
{
    public const string Healthy = "Healthy";
    public const string Unhealthy = "Unhealthy";
    public const string Unknown = "Unknown";
    public const string NotSupported = "NotSupported";

    public static bool IsValid(string value) => value is Healthy or Unhealthy or Unknown or NotSupported;
}
