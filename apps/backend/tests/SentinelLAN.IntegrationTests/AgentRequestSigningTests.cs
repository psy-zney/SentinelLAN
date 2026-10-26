using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelLAN.Agent.Infrastructure;
using SentinelLAN.Api;
using SentinelLAN.Api.Endpoints;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;
using Identity = SentinelLAN.Agent.Core.DeviceIdentity;

namespace SentinelLAN.IntegrationTests;

public sealed class AgentRequestSigningTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private WebApplicationFactory<Program> CreateApi(bool allowDevelopmentLegacy = false) => factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<AgentRequestAuthenticationSettings>();
        services.AddSingleton(new AgentRequestAuthenticationSettings(allowDevelopmentLegacy, null));
    }));

    [Theory]
    [InlineData("0.2.24")]
    [InlineData("1.0.2")]
    [InlineData("9.9.0")]
    public async Task SignedRequestsInteroperateWithAgentAndReplayIsRejectedWithoutTransmittingTheDeviceSecret(string agentVersion)
    {
        using var api = CreateApi();
        using var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var identity = await SeedAsync(api);
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { idempotencyKey = "signed-once", cpuPercent = 10, ramPercent = 20, diskPercent = 30, osVersion = "Fixture", agentVersion });
        var headers = AgentRequestSigner.Headers(identity, "POST", "/api/v1/agent/heartbeat", payload);
        Assert.DoesNotContain("X-SentinelLAN-Device-Secret", headers.Keys);
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(Request("/api/v1/agent/heartbeat", payload, headers))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("/api/v1/agent/heartbeat", payload, headers))).StatusCode);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Single(await db.Heartbeats.Where(value => value.DeviceId == identity.DeviceId).ToListAsync());
        Assert.Single(await db.Set<AgentRequestNonce>().Where(value => value.DeviceId == identity.DeviceId).ToListAsync());
        var credential = await db.DeviceCredentials.SingleAsync(value => value.DeviceId == identity.DeviceId);
        Assert.NotNull(credential.RequestSigningConfirmedAt);
        Assert.True(credential.RequestSigningKeyEnvelope!.AsSpan().IndexOf(Convert.FromBase64String(identity.RequestSigningKey!)) < 0);
        Assert.NotEqual(identity.DeviceSecret, credential.SecretHash);
    }

    [Theory]
    [InlineData("body")]
    [InlineData("method")]
    [InlineData("path")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("device")]
    public async Task ModifiedOrExpiredSignedRequestsFailBeforeAnEndpointExecutes(string change)
    {
        using var api = CreateApi();
        using var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var identity = await SeedAsync(api);
        byte[] original = Encoding.UTF8.GetBytes("{\"idempotencyKey\":\"tamper\",\"cpuPercent\":10,\"ramPercent\":20,\"diskPercent\":30,\"osVersion\":\"Fixture\",\"agentVersion\":\"test\"}");
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(change == "expired" ? -120 : change == "future" ? 120 : 0);
        var headers = new Dictionary<string, string>(AgentRequestSigner.Headers(identity, change == "method" ? "GET" : "POST",
            change == "path" ? "/api/v1/agent/commands/poll" : "/api/v1/agent/heartbeat", original, new Clock(timestamp)));
        if (change == "device") headers["X-SentinelLAN-Device-Id"] = Guid.NewGuid().ToString("D");
        var body = change == "body" ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace("10", "99", StringComparison.Ordinal)) : original;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("/api/v1/agent/heartbeat", body, headers))).StatusCode);
        using var scope = api.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<SentinelDbContext>().Heartbeats.Where(value => value.DeviceId == identity.DeviceId).ToListAsync());
    }

    [Fact]
    public async Task BootstrapRecoversALostResponseThenClosesAfterFirstSignedProofAndLegacyCannotDowngrade()
    {
        using var api = CreateApi(allowDevelopmentLegacy: true);
        using var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var identity = await SeedAsync(api);
        var proof = new UpgradeAgentRequestSigningRequest(identity.DeviceId, identity.DeviceSecret);
        var first = await client.PostAsJsonAsync("/api/v1/agent/auth/upgrade", proof);
        var recovered = await client.PostAsJsonAsync("/api/v1/agent/auth/upgrade", proof);
        Assert.Equal(identity.RequestSigningKey, (await first.Content.ReadFromJsonAsync<UpgradeAgentRequestSigningResponse>())!.RequestSigningKey);
        Assert.Equal(identity.RequestSigningKey, (await recovered.Content.ReadFromJsonAsync<UpgradeAgentRequestSigningResponse>())!.RequestSigningKey);
        var signed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/agent/policy");
        foreach (var (key, value) in AgentRequestSigner.Headers(identity, "GET", "/api/v1/agent/policy", [])) signed.Headers.Add(key, value);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(signed)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/agent/auth/upgrade", proof)).StatusCode);
        var legacy = new HttpRequestMessage(HttpMethod.Get, "/api/v1/agent/policy");
        legacy.Headers.Add("X-SentinelLAN-Device-Id", identity.DeviceId.ToString());
        legacy.Headers.Add("X-SentinelLAN-Device-Secret", identity.DeviceSecret);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(legacy)).StatusCode);
    }

    private static async Task<Identity> SeedAsync(WebApplicationFactory<Program> api)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var fixtureId = Guid.NewGuid().ToString("N");
        var org = new Organization { Code = $"signing-{fixtureId}", Name = $"Request signing fixture {fixtureId}" };
        var device = new Device { OrganizationId = org.Id, Name = "Signed fixture", OsVersion = "Fixture", AgentVersion = "test" };
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var envelope = AgentRequestKey.Create(scope.ServiceProvider.GetRequiredService<SensitiveDataCipher>(), org.Id, device.Id, out var encoded);
        db.AddRange(org, device, new DeviceCredential { OrganizationId = org.Id, DeviceId = device.Id, SecretHash = SecretHash.Create(secret), RequestSigningKeyEnvelope = envelope });
        await db.SaveChangesAsync();
        return new(device.Id, secret, encoded);
    }
    private static HttpRequestMessage Request(string path, byte[] body, IReadOnlyDictionary<string, string> headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new("application/json");
        foreach (var (key, value) in headers) request.Headers.Add(key, value);
        return request;
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
