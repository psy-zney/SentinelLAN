namespace SentinelLAN.Api;

public sealed record DeploymentSettings(bool PlatformEnabled)
{
    public static DeploymentSettings FromConfiguration(IConfiguration configuration)
    {
        var mode = configuration["SENTINELLAN_DEPLOYMENT_MODE"] ?? "Platform";
        return mode.ToLowerInvariant() switch
        {
            "selfhost" => new(false),
            "platform" => new(true),
            _ => throw new InvalidOperationException("SENTINELLAN_DEPLOYMENT_MODE must be SelfHost or Platform.")
        };
    }
}
