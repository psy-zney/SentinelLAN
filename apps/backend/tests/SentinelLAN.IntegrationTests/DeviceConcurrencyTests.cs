using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class DeviceConcurrencyTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task ParallelEnrollmentConsumesTokenOnlyOnce()
    {
        var token = Guid.NewGuid().ToString("N");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var organization = await db.Organizations.FirstAsync();
            db.Add(new DeviceEnrollmentToken { OrganizationId = organization.Id, TokenHash = SecretHash.Create(token), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        var request = new EnrollRequest(token, $"Concurrent enrollment {Guid.NewGuid():N}", "Windows", "test");
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync("/api/v1/agent/enroll", request)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        await using var verification = factory.Services.CreateAsyncScope();
        var store = verification.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Equal(1, await store.Devices.CountAsync(device => device.Name == request.DeviceName));
    }

    [Fact]
    public async Task StaleEnrollmentContextCannotConsumeTokenAgain()
    {
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var token = new DeviceEnrollmentToken { OrganizationId = (await first.Organizations.FirstAsync()).Id, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) };
        first.Add(token);
        await first.SaveChangesAsync();
        var stale = await second.EnrollmentTokens.SingleAsync(item => item.Id == token.Id);
        Assert.True(token.TryUse(DateTimeOffset.UtcNow));
        Assert.True(stale.TryUse(DateTimeOffset.UtcNow));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task ParallelHeartbeatRetriesCreateOnlyOneSnapshotAndInvalidMetricsCreateNone()
    {
        using var client = factory.CreateClient();
        var device = new Device { OrganizationId = Guid.NewGuid(), Name = "Concurrent heartbeat", OsVersion = "Windows", AgentVersion = "test" };
        var secret = Guid.NewGuid().ToString("N");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            db.Add(device);
            db.Add(new DeviceCredential { OrganizationId = device.OrganizationId, DeviceId = device.Id, SecretHash = SecretHash.Create(secret) });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Id", device.Id.ToString());
        client.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Secret", secret);
        var heartbeat = new HeartbeatRequest("retry-once", 25, 50, 75, "Windows", "test");
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync("/api/v1/agent/heartbeat", heartbeat)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Accepted);
        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/heartbeat", heartbeat with { IdempotencyKey = "invalid", CpuPercent = 101 })).StatusCode);
        await using var verification = factory.Services.CreateAsyncScope();
        var store = verification.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Equal(1, await store.Telemetry.CountAsync(item => item.DeviceId == device.Id));
    }
}
