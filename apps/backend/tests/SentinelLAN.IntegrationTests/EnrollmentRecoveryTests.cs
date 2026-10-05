using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class EnrollmentRecoveryTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task LostResponseCanBeRecoveredOnlyWithOriginalDeviceProof()
    {
        using var client = factory.CreateClient();
        var request = await SeedAsync();
        var original = await client.PostAsJsonAsync("/api/v1/agent/enroll", request);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var identity = (await original.Content.ReadFromJsonAsync<EnrollResponse>())!;
        Assert.Equal(request.DeviceSecret, identity.DeviceSecret);
        var retry = await client.PostAsJsonAsync("/api/v1/agent/enroll", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(identity, await retry.Content.ReadFromJsonAsync<EnrollResponse>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/enroll", request with { DeviceSecret = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/enroll", request with { DeviceSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/enroll", request with { DeviceName = "Different device" })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Single(await db.Devices.Where(d => d.Name == request.DeviceName).ToListAsync());
        Assert.DoesNotContain(await db.AuditLogs.Where(a => a.DeviceId == identity.DeviceId).ToListAsync(),
            a => a.Reason.Contains(request.DeviceSecret!, StringComparison.Ordinal) || a.Reason.Contains(request.Token, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationOrTokenExpiryPreventsRecovery(bool expire)
    {
        using var client = factory.CreateClient();
        var request = await SeedAsync();
        var original = await client.PostAsJsonAsync("/api/v1/agent/enroll", request);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var identity = (await original.Content.ReadFromJsonAsync<EnrollResponse>())!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            if (expire)
            {
                var token = await db.EnrollmentTokens.SingleAsync(t => t.TokenHash == SecretHash.Create(request.Token));
                db.Entry(token).Property(t => t.ExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
            }
            else (await db.DeviceCredentials.SingleAsync(c => c.DeviceId == identity.DeviceId)).RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/enroll", request)).StatusCode);
    }

    [Fact]
    public async Task SameProofConcurrentRetriesCreateOneDeviceWhileDifferentProofIsRejected()
    {
        using var client = factory.CreateClient();
        var request = await SeedAsync();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsJsonAsync("/api/v1/agent/enroll", request)));
        var identities = new List<EnrollResponse>();
        foreach (var response in attempts)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            identities.Add((await response.Content.ReadFromJsonAsync<EnrollResponse>())!);
        }
        Assert.Single(identities.Select(i => i.DeviceId).Distinct());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/enroll",
            request with { DeviceSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) })).StatusCode);
    }

    private async Task<EnrollRequest> SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var organization = await db.Organizations.SingleAsync(o => o.Code == "demo");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Add(new DeviceEnrollmentToken { OrganizationId = organization.Id, TokenHash = SecretHash.Create(token), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) });
        await db.SaveChangesAsync();
        return new EnrollRequest(token, $"Recovery-{Guid.NewGuid():N}", "Windows", "test", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }
}
