using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AdminHostMonitoringTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private const string Path = "/api/v1/host/status";
    private static VpsHostSnapshotDto Snapshot(DateTimeOffset captured) => new("lab-vps", "192.0.2.1", captured, "Linux", 1000,
        2, 25, 40, 1024, 256, 4096, 1638,
        new("running", true, null, [new("docker", "active", "enabled")],
            [new("abc", "sentinellan-prod-api-1", "api:1", "running", "Up", null, "unless-stopped", 2,
                "1MiB / 4MiB", 25, "1MB", "1MB / 2MB", "0B / 0B",
                [new(8080, "tcp", null, null)], [new("*", 8443, "tcp")], "bridge")]),
        [new("127.0.0.1", 9003, "tcp", "docker-proxy")], []);

    private sealed class Reader(VpsHostSnapshotDto? snapshot, bool configured = true) : IVpsHostSnapshotReader
    {
        public bool Configured => configured;
        public int Reads { get; private set; }
        public Task<VpsHostSnapshotDto?> ReadAsync(CancellationToken ct)
        {
            Reads++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateApi(Reader reader, string? organization = "demo", TimeProvider? clock = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVpsHostSnapshotReader>();
            services.AddSingleton<IVpsHostSnapshotReader>(reader);
            services.RemoveAll<VpsHostMonitorSettings>();
            services.AddSingleton(new VpsHostMonitorSettings(organization));
            if (clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            }
        }));

    [Fact]
    public async Task OnlyOperatingOrganizationAdminCanReadHostAndResponseIsNotCached()
    {
        var reader = new Reader(Snapshot(DateTimeOffset.UtcNow));
        using var api = CreateApi(reader);
        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);
        using var employee = api.CreateClient();
        employee.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync(Path)).StatusCode);
        Assert.Equal(0, reader.Reads);
        using var admin = api.CreateClient();
        admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        var response = await admin.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var status = (await response.Content.ReadFromJsonAsync<VpsHostStatusDto>())!;
        Assert.True(status.Available);
        Assert.False(status.Stale);
        Assert.Equal(9003, Assert.Single(status.Snapshot!.ListeningPorts).Port);
        Assert.Equal(8443, Assert.Single(status.Snapshot.Runtime.Containers[0].ListeningPorts!).Port);
        Assert.Null(status.Snapshot.Runtime.Containers[0].Ports![0].HostPort);
        var document = await anonymous.GetStringAsync("/openapi/v1.json");
        Assert.Contains(Path, document);
        Assert.Contains("VpsHostStatusDto", document);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/platform/host/status")).StatusCode);
    }

    [Fact]
    public async Task AdminFromAnotherOrganizationIsDeniedBeforeReadingHostData()
    {
        var reader = new Reader(Snapshot(DateTimeOffset.UtcNow));
        using var api = CreateApi(reader, "different-operating-organization");
        using var admin = api.CreateClient();
        admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(Path)).StatusCode);
        Assert.Equal(0, reader.Reads);
    }

    [Theory]
    [InlineData(-30, false)]
    [InlineData(-181, true)]
    [InlineData(61, true)]
    public async Task OldOrFutureSamplesAreNeverShownAsCurrent(int offsetSeconds, bool stale)
    {
        var now = DateTimeOffset.UtcNow;
        using var api = CreateApi(new Reader(Snapshot(now.AddSeconds(offsetSeconds))), clock: new Clock(now));
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var admin = await db.Users.SingleAsync(user => user.Email == "admin@sentinellan.local");
        var service = scope.ServiceProvider.GetRequiredService<VpsHostMonitorService>();
        var actor = new ActorContext(admin.Id, admin.OrganizationId, Roles.Admin);
        var result = await service.GetStatusAsync(actor, CancellationToken.None);
        Assert.True(result.Available);
        Assert.Equal(stale, result.Stale);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetStatusAsync(actor, new CancellationToken(true)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStatusAsync(actor with { Role = Roles.Employee }, CancellationToken.None));
    }

    [Theory]
    [InlineData(false, "demo", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "demo", true)]
    public async Task MissingAndUnconfiguredMonitoringStayDistinct(bool readerConfigured, string? organization, bool expectedConfigured)
    {
        using var api = CreateApi(new Reader(null, readerConfigured), organization);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var admin = await db.Users.SingleAsync(user => user.Email == "admin@sentinellan.local");
        var result = await scope.ServiceProvider.GetRequiredService<VpsHostMonitorService>()
            .GetStatusAsync(new(admin.Id, admin.OrganizationId, Roles.Admin), CancellationToken.None);
        Assert.Equal(expectedConfigured, result.Configured);
        Assert.False(result.Available);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ReaderRejectsInvalidFilesAndDoesNotReturnUnknownSensitiveFields()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"host-snapshot-{Guid.NewGuid():N}.json");
        var reader = new FileVpsHostSnapshotReader(path);
        try
        {
            Assert.Null(await reader.ReadAsync(CancellationToken.None));
            foreach (var content in new[] { "{invalid", "{}", new string('x', 2 * 1024 * 1024 + 1) })
            {
                await File.WriteAllTextAsync(path, content);
                Assert.Null(await reader.ReadAsync(CancellationToken.None));
            }
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(Snapshot(DateTimeOffset.UtcNow), options);
            await File.WriteAllTextAsync(path, json[..^1] + ",\"environment\":\"private-fixture-value\"}");
            var snapshot = await reader.ReadAsync(CancellationToken.None);
            Assert.NotNull(snapshot);
            Assert.DoesNotContain("private-fixture-value", JsonSerializer.Serialize(snapshot, options));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(new CancellationToken(true)));
        }
        finally { File.Delete(path); }
    }
}
