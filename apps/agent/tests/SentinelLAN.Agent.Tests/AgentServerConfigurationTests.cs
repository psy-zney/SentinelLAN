using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class AgentServerConfigurationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(null, true)]
    public void MissingServerNeverSilentlyFallsBackToLocalhost(string? configured, bool development) =>
        Assert.Throws<InvalidOperationException>(() => AgentServerConfiguration.Resolve(configured, null, development));

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("https://localhost")]
    [InlineData("http://192.168.1.2")]
    [InlineData("https://user:password@server.test")]
    [InlineData("https://server.test/api")]
    [InlineData("https://server.test/?token=secret")]
    [InlineData("https://server.test/#secret")]
    public void ProductionRejectsLoopbackAndUnsafeOrigins(string url) =>
        Assert.Throws<InvalidOperationException>(() => AgentServerConfiguration.Resolve(url, null, false));

    [Fact]
    public void CompanyBuildWorksWithoutAnEnvironmentVariableAndPinsTheDestination()
    {
        var server = AgentServerConfiguration.Resolve(null, "https://manager.zney295.id.vn", false);
        Assert.Equal("https://manager.zney295.id.vn/", server.AbsoluteUri);
        Assert.Equal(server, AgentServerConfiguration.Resolve("https://manager.zney295.id.vn/", "https://manager.zney295.id.vn", false));
        Assert.Throws<InvalidOperationException>(() => AgentServerConfiguration.Resolve("https://other.test", server.AbsoluteUri, false));
        Assert.Throws<InvalidOperationException>(() => AgentServerConfiguration.Resolve("http://localhost:8080", server.AbsoluteUri, true));
    }

    [Fact]
    public void DevelopmentLoopbackRequiresAnExplicitUrlAndNoCompanyPin() =>
        Assert.Equal("http://localhost:8080/", AgentServerConfiguration.Resolve("http://localhost:8080", null, true).AbsoluteUri);
}
