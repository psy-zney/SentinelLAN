using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class MonitoringSettingsTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private const string Route = "/api/v1/organization/monitoring-settings";

    [Fact]
    public async Task OnlyAdminCanConfigureThresholdsAndStaleOrReplayedUpdatesAreRejected()
    {
        using var employee = factory.CreateClient();
        await employee.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync(Route)).StatusCode);
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var initial = (await admin.GetFromJsonAsync<MonitoringSettingsDto>(Route))!;
        var input = new UpdateMonitoringSettingsRequest(initial with { CpuThresholdPercent = 20, SustainedMinutes = 0 },
            "Thử cảnh báo trên mẫu mới nhất", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        var response = await admin.PutAsJsonAsync(Route, input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<MonitoringSettingsDto>())!;
        Assert.Equal(20, saved.CpuThresholdPercent);
        Assert.Equal(0, saved.SustainedMinutes);
        Assert.NotEqual(initial.Revision, saved.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(Route, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(Route, input with { Nonce = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Route,
            input with { Settings = saved with { MaintenancePauseMinutes = 61 }, Nonce = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Route,
            input with { Settings = saved, Confirmed = false, Nonce = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Route,
            input with { Settings = saved, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), Nonce = Guid.NewGuid() })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Single(await db.Set<OrganizationMonitoringSettings>().ToListAsync());
        Assert.Single(await db.AuditLogs.Where(log => log.Action == "MonitoringSettingsUpdated").ToListAsync());
        var other = Guid.NewGuid();
        var service = scope.ServiceProvider.GetRequiredService<MonitoringSettingsService>();
        var otherSettings = await service.GetAsync(new(Guid.NewGuid(), other, Roles.Admin), default);
        Assert.Equal(90, otherSettings.CpuThresholdPercent);
        Assert.Equal(Guid.Empty, otherSettings.Revision);
    }
}
