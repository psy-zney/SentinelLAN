using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using SentinelLAN.Agent.Infrastructure;
using SentinelLAN.Application;

namespace SentinelLAN.IntegrationTests;

public sealed class AgentWakeIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task PersistedCommandWakesOnlyItsAuthenticatedAgentWithoutSendingTheCommandBody()
    {
        using var admin = factory.CreateClient(); admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var tokenResponse = await admin.PostAsJsonAsync("/api/v1/enrollment-tokens", new EnrollmentTokenRequest(15, "Wake fixture", true));
        var token = (await tokenResponse.Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!.Token;
        using var transport = factory.CreateClient(); using var api = new AgentApi(transport, "WAKE-FIXTURE");
        var identity = await api.EnrollAsync(token, default);
        await api.GetSoftwareRulesAsync(identity, default); // Confirms possession of the new request key.
        var sockets = factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = request => { foreach (var header in AgentRequestSigner.Headers(identity, "GET", "/hubs/agent-wake", [])) request.Headers[header.Key] = header.Value; };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var socket = await sockets.ConnectAsync(new Uri("ws://localhost/hubs/agent-wake"), timeout.Token);
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e")), WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[4096]; await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        var command = await admin.PostAsJsonAsync("/api/v1/commands/bulk", new BulkCommandRequest([identity.DeviceId], "CollectTelemetryNow",
            new("Private authorized fixture reason", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2))));
        command.EnsureSuccessStatusCode();
        var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        var text = Encoding.UTF8.GetString(buffer, 0, received.Count);
        Assert.Contains("wake-up", text); Assert.DoesNotContain("Private authorized fixture reason", text); Assert.DoesNotContain("CollectTelemetryNow", text);
        Assert.NotNull(await api.PollCommandAsync(identity, default));
    }
}
