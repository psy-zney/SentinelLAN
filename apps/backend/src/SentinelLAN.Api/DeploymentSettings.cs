namespace SentinelLAN.Api;

public sealed record DeploymentSettings
{
    public static DeploymentSettings FromConfiguration(IConfiguration configuration)
    {
        var mode = configuration["SENTINELLAN_DEPLOYMENT_MODE"] ?? "SelfHost";
        if (!string.Equals(mode, "SelfHost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only SelfHost deployment is supported. Remove the obsolete Platform configuration.");
        return new DeploymentSettings();
    }
}
