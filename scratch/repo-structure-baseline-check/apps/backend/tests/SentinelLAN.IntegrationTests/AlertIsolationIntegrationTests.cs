using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AlertIsolationIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task ForeignDeviceReferenceIsRejectedAndPreexistingInvalidReferenceDoesNotLeakName()
    {
        var setup = await SeedForeignDeviceAndInvalidAlertAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
        {
            Content = JsonContent.Create(new CreateAlertRequest(setup.ForeignDeviceId, "Warning", "Foreign device alert attempt"))
        };
        request.Headers.Add("X-SentinelLAN-CSRF", "1");
        var rejected = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var alerts = await client.GetFromJsonAsync<AlertDto[]>("/api/v1/alerts");
        Assert.NotNull(alerts);
        var invalidReference = Assert.Single(alerts, alert => alert.Id == setup.InvalidAlertId);
        Assert.Equal(setup.ForeignDeviceId, invalidReference.DeviceId);
        Assert.Null(invalidReference.DeviceName);
        Assert.DoesNotContain(alerts, alert => alert.DeviceName == setup.ForeignDeviceName);
    }

    private async Task<(Guid ForeignDeviceId, Guid InvalidAlertId, string ForeignDeviceName)> SeedForeignDeviceAndInvalidAlertAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var demo = await db.Organizations.SingleAsync(organization => organization.Code == "demo");
        var foreign = new Organization { Code = $"alert-foreign-{Guid.NewGuid():N}", Name = "Alert Foreign Tenant" };
        var foreignName = $"FOREIGN-ALERT-DEVICE-{Guid.NewGuid():N}";
        var foreignDevice = new Device
        {
            OrganizationId = foreign.Id,
            Name = foreignName,
            OsVersion = "Windows 11",
            AgentVersion = "test"
        };
        var invalidAlert = new Alert
        {
            OrganizationId = demo.Id,
            DeviceId = foreignDevice.Id,
            Severity = "Warning",
            Message = "Preexisting invalid device reference",
            IsOpen = true
        };
        db.AddRange(foreign, foreignDevice, invalidAlert);
        await db.SaveChangesAsync();
        return (foreignDevice.Id, invalidAlert.Id, foreignName);
    }
}
