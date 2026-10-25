using System.Reflection;

namespace SentinelLAN.Agent.Infrastructure;

public static class AgentBuildVersion
{
    public static string Current => typeof(AgentBuildVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion.Split('+')[0] ?? typeof(AgentBuildVersion).Assembly.GetName().Version?.ToString(3) ?? "unknown";
}
