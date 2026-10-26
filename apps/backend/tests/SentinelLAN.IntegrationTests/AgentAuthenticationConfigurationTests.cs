using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SentinelLAN.Api;

namespace SentinelLAN.IntegrationTests;

public sealed class AgentAuthenticationConfigurationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Theory]
    [InlineData("SENTINELLAN_AGENT_REQUIRE_SIGNED_REQUESTS")]
    [InlineData("SENTINELLAN_REQUIRE_SIGNED_AGENT_REQUESTS")]
    public void BothDocumentedAndLegacySettingsDisableDevelopmentUnsignedAuthentication(string name)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [name] = "true" }).Build();
        var environment = factory.Services.GetRequiredService<IHostEnvironment>();
        Assert.False(AgentRequestAuthenticationSettings.FromConfiguration(configuration, environment).DevelopmentLegacyEnabled);
    }
}
