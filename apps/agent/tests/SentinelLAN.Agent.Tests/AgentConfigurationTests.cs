using Microsoft.Extensions.Configuration;
using SentinelLAN.Agent;

namespace SentinelLAN.Agent.Tests;

public sealed class AgentConfigurationTests
{
    [Fact]
    public void ExplicitEnvironmentAndCommandLineOverrideInstallerDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sentinellan-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        var key = "SENTINELLAN_TEST_" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(path, "{\"" + key + "\":\"installer\"}");
            using var fromFile = (ConfigurationRoot)new ConfigurationBuilder().AddAgentSettings(path, []).Build();
            Assert.Equal("installer", fromFile[key]);
            Environment.SetEnvironmentVariable(key, "environment");
            using var fromEnvironment = (ConfigurationRoot)new ConfigurationBuilder().AddAgentSettings(path, []).Build();
            Assert.Equal("environment", fromEnvironment[key]);
            using var fromArguments = (ConfigurationRoot)new ConfigurationBuilder().AddAgentSettings(path, ["--" + key, "argument"]).Build();
            Assert.Equal("argument", fromArguments[key]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
