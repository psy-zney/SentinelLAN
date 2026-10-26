using System.Net;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class OptionalEndpointCompatibilityTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task MissingOptionalFeaturesDoNotPreventSignedHeartbeat(HttpStatusCode status)
    {
        var handler = new CompatibilityHandler(status);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://company.test") };
        using var api = new AgentApi(client, "Fixture");
        var identity = new DeviceIdentity(Guid.NewGuid(), new string('A', 64), Convert.ToBase64String(new byte[32]));
        Assert.Null(await api.PollCommandAsync(identity, CancellationToken.None));
        Assert.Null(await api.GetPolicyAsync(identity, CancellationToken.None));
        await api.SendHeartbeatAsync(identity, new TelemetrySnapshot(1, 2, 3, "Fixture", "0.2.24"), CancellationToken.None);
        Assert.Equal(3, handler.SignedRequests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AuthenticationAndServerFailuresAreNotHiddenAsCompatibility(HttpStatusCode status)
    {
        using var client = new HttpClient(new CompatibilityHandler(status)) { BaseAddress = new Uri("https://company.test") };
        using var api = new AgentApi(client, "Fixture");
        var identity = new DeviceIdentity(Guid.NewGuid(), new string('A', 64), Convert.ToBase64String(new byte[32]));
        await Assert.ThrowsAsync<HttpRequestException>(() => api.PollCommandAsync(identity, CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => api.GetPolicyAsync(identity, CancellationToken.None));
    }

    private sealed class CompatibilityHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int SignedRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.False(request.Headers.Contains("X-SentinelLAN-Device-Secret"));
            Assert.True(request.Headers.Contains("X-SentinelLAN-Signature"));
            SignedRequests++;
            return Task.FromResult(new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("/heartbeat", StringComparison.Ordinal)
                ? HttpStatusCode.Accepted : status));
        }
    }
}
