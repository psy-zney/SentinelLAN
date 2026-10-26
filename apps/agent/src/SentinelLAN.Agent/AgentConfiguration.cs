using Microsoft.Extensions.Configuration;

namespace SentinelLAN.Agent;

public static class AgentConfiguration
{
    public static IConfigurationBuilder AddAgentSettings(this IConfigurationBuilder configuration, string settingsPath, string[] args)
    {
        // Persisted installer settings are defaults. Explicit service environment and CLI win.
        return configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables().AddCommandLine(args);
    }
}
