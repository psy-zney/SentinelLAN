using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Agent.Infrastructure;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;
using Core = SentinelLAN.Agent.Core;

namespace SentinelLAN.IntegrationTests;

public sealed class SoftwareComplianceTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task SignedReportsUseTenantRulesAndUnknownNeverClearsAProhibitedDetection()
    {
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var request = new SaveProhibitedSoftwareRuleRequest("Fixture restricted MSI", Guid.NewGuid(), null, true, Guid.Empty,
            "Configured detection fixture", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        var created = await admin.PostAsJsonAsync("/api/v1/software/prohibited-rules", request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var rule = (await created.Content.ReadFromJsonAsync<ProhibitedSoftwareRuleDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/software/prohibited-rules", request)).StatusCode);
        var enrollment = await admin.PostAsJsonAsync("/api/v1/enrollment-tokens", new EnrollmentTokenRequest(15, "Software test", true));
        var token = (await enrollment.Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!.Token;
        using var transport = factory.CreateClient();
        using var api = new AgentApi(transport, "SOFTWARE-TEST");
        var identity = await api.EnrollAsync(token, default);
        Assert.Contains(await api.GetSoftwareRulesAsync(identity, default), value => value.Id == rule.Id && value.ProductCode == request.ProductCode);
        await Report("Installed", "1.0");
        await Report("Unknown", null);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Single(await db.Alerts.Where(value => value.DeviceId == identity.DeviceId && value.IsOpen).ToListAsync());
        Assert.Single(await db.SecurityEvents.Where(value => value.DeviceId == identity.DeviceId && value.Type == "ProhibitedSoftware").ToListAsync());
        Assert.Null(await scope.ServiceProvider.GetRequiredService<SoftwareComplianceService>().StatusAsync(new(Guid.NewGuid(), Guid.NewGuid(), Roles.Admin), identity.DeviceId, default));
        await Report("NotFound", null);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Alerts.Where(value => value.DeviceId == identity.DeviceId && value.IsOpen).ToListAsync());
        var oldRevision = rule.Revision;
        var changed = await admin.PutAsJsonAsync($"/api/v1/software/prohibited-rules/{rule.Id}", request with { Revision = rule.Revision, Nonce = Guid.NewGuid(), IsActive = false });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await Report("Installed", "1.0"); // A queued sample for a retired rule must not block the heartbeat.
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Alerts.Where(value => value.DeviceId == identity.DeviceId && value.IsOpen).ToListAsync());

        Task Report(string detection, string? version) => api.SendHeartbeatAsync(identity,
            new Core.TelemetrySnapshot(10, 20, 30, "Fixture", "test", CollectedAt: DateTimeOffset.UtcNow,
                Software: [new(rule.Id, "Prohibited", detection, version, DateTimeOffset.UtcNow, rule.Revision)]), default);
    }

    [Theory]
    [InlineData("1.0", "1.0.0", "Installed")]
    [InlineData("1.0", "2.0", "UpdateAvailable")]
    [InlineData(null, "2.0", "VersionUnknown")]
    public void VersionComparisonIsConservative(string? installed, string required, string expected) => Assert.Equal(expected, SoftwareComplianceService.VersionStatus(installed, required));
}
