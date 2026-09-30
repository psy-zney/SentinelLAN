using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class VpsOperationsApiTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private sealed class FakeSsh : IVpsSshService
    {
        public int Commands { get; private set; }
        public Task<VpsConnectionTestResultDto> TestConnectionAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default) => Task.FromResult(new VpsConnectionTestResultDto(true, "Connected"));
        public Task<VpsMetricsResultDto> CollectMetricsAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VpsMetricsResultDto(true, "Collected", CpuPercent: 20, Runtime: new("running", true, null, [new("docker", "active", "enabled")], [])));
        public Task<VpsCommandResultDto> RestartServiceAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, string serviceName, CancellationToken cancellationToken = default) => Task.FromResult(new VpsCommandResultDto(true, "Restarted"));
        public Task<VpsCommandResultDto> ExecuteOperationAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, VpsOperationRequest request, CancellationToken cancellationToken = default)
        {
            Commands++;
            return Task.FromResult(new VpsCommandResultDto(true, "Scheduled"));
        }
    }

    [Fact]
    public async Task OnlyOwnerCanMonitorAndScheduleSingleUseOperationsAndOpenApiIncludesContracts()
    {
        var ssh = new FakeSsh();
        using var api = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVpsSshService>();
            services.AddSingleton<IVpsSshService>(ssh);
        }));
        Guid id;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            const string ownerEmail = "vps-owner@platform.test";
            const string ownerPassword = "Vps-integration-owner-secret-2026!";
            await PlatformOwnerInitializer.EnsureCreatedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), ownerEmail, ownerPassword);
            var vault = scope.ServiceProvider.GetRequiredService<IVpsVaultService>();
            var node = new VpsNode { Name = $"API-Lab-{Guid.NewGuid():N}", Host = "192.0.2.1", Username = "operator", HostKeyFingerprint = "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", EncryptedPrivateKey = vault.Encrypt("fake-key") };
            db.Add(node); await db.SaveChangesAsync(); id = node.Id;
        }
        var request = new VpsOperationRequest("Reboot", null, null, "Authorized lab maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        using var anonymous = api.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/v1/platform/vps-nodes/{id}/operations", request)).StatusCode);
        using var company = api.CreateClient();
        company.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/platform/vps-nodes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsJsonAsync($"/api/v1/platform/vps-nodes/{id}/refresh-metrics", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsJsonAsync($"/api/v1/platform/vps-nodes/{id}/operations", request)).StatusCode);
        Assert.Equal(0, ssh.Commands);

        using var owner = api.CreateClient();
        owner.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformLoginRequest("vps-owner@platform.test", "Vps-integration-owner-secret-2026!"))).StatusCode);
        var snapshot = await owner.PostAsJsonAsync($"/api/v1/platform/vps-nodes/{id}/refresh-metrics", new { });
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        var nodeDto = (await snapshot.Content.ReadFromJsonAsync<VpsNodeDto>())!;
        Assert.Equal("running", nodeDto.Runtime!.SystemState);
        Assert.Equal(20, nodeDto.CpuPercent);
        Assert.DoesNotContain("fake-key", await snapshot.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/v1/platform/vps-nodes/{id}/operations", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/v1/platform/vps-nodes/{id}/operations", request)).StatusCode);
        Assert.Equal(1, ssh.Commands);
        using (var scope = api.Services.CreateScope())
        {
            var audits = await scope.ServiceProvider.GetRequiredService<SentinelDbContext>().AuditLogs.IgnoreQueryFilters().Where(a => a.Action == "VpsOperationCompleted").ToListAsync();
            Assert.Contains(audits, a => a.Reason.Contains(id.ToString(), StringComparison.Ordinal) && a.Outcome == "Scheduled");
        }
        var openApi = await anonymous.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/v1/platform/vps-nodes/{id}/operations", openApi);
        Assert.Contains("VpsOperationRequest", openApi);
        Assert.Contains("VpsRuntimeDto", openApi);
    }
}
