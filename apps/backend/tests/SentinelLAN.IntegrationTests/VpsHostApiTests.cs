using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class VpsHostApiTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private sealed class Reader : IVpsHostSnapshotReader
    {
        public bool Configured => true;
        public int Reads { get; private set; }
        public Task<VpsHostSnapshotDto?> ReadAsync(CancellationToken ct) { Reads++; return Task.FromResult<VpsHostSnapshotDto?>(VpsHostMonitorTests.Snapshot(DateTimeOffset.UtcNow)); }
    }

    [Fact]
    public async Task OnlyPlatformCookieCanReadPortsAndResponseIsNotCached()
    {
        var reader = new Reader();
        using var api = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVpsHostSnapshotReader>(); services.AddSingleton<IVpsHostSnapshotReader>(reader);
        }));
        using (var scope = api.Services.CreateScope())
            await PlatformOwnerInitializer.EnsureCreatedAsync(scope.ServiceProvider.GetRequiredService<SentinelDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), "host-owner@platform.test", "Host-integration-owner-secret-2026!");
        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/platform/host/status")).StatusCode);
        using var company = api.CreateClient(); company.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/platform/host/status")).StatusCode);
        Assert.Equal(0, reader.Reads);
        using var owner = api.CreateClient(); owner.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformLoginRequest("host-owner@platform.test", "Host-integration-owner-secret-2026!"))).StatusCode);
        var response = await owner.GetAsync("/api/v1/platform/host/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var status = (await response.Content.ReadFromJsonAsync<VpsHostStatusDto>())!;
        Assert.True(status.Available); Assert.False(status.Stale);
        Assert.Equal(9000, Assert.Single(status.Snapshot!.ListeningPorts).Port);
        Assert.Equal(3180, status.Snapshot.Runtime.Containers[0].Ports![0].HostPort);
        var openApi = await anonymous.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/v1/platform/host/status", openApi); Assert.Contains("VpsHostStatusDto", openApi);
    }
}
