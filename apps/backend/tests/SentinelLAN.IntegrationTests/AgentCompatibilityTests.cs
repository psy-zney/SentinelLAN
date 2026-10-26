using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SentinelLAN.Api;

namespace SentinelLAN.IntegrationTests;

public sealed class AgentCompatibilityTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task PublicProtocolInformationDoesNotRequireExactVersionsOrDiscloseEnvironment()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/agent/compatibility");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, data.GetProperty("protocolVersion").GetInt32());
        Assert.False(data.GetProperty("exactVersionRequired").GetBoolean());
        Assert.Contains("hmac-sha256-v1", data.GetProperty("authentication").EnumerateArray().Select(value => value.GetString()));
        Assert.False(data.TryGetProperty("environment", out _));
        Assert.False(data.TryGetProperty("connectionString", out _));
        Assert.True(response.Headers.CacheControl!.NoStore);
    }
}
