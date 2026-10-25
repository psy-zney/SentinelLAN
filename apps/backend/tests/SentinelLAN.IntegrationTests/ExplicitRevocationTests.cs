using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Api;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class ExplicitRevocationTests
{
    [Theory]
    [InlineData("/api/v1/users/{id}/activation-token")]
    [InlineData("/api/v1/devices/{id}/qr-label")]
    public async Task OpenApiRequiresTheExplicitRevocationBody(string path)
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var body = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("delete").GetProperty("requestBody");
        Assert.True(body.GetProperty("required").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationRequiresExplicitBodyReasonAndConfirmationBeforePersisting(bool qr)
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        client.DefaultRequestHeaders.Add(AuthCookieManager.CsrfHeaderName, "1");
        Guid targetId;
        Guid entityId;
        string path;
        var action = qr ? "DeviceQrLabelRevoked" : "ActivationTokenRevoked";
        if (qr)
        {
            var devices = await client.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices");
            Assert.NotNull(devices);
            targetId = devices.First().Id;
            path = $"/api/v1/devices/{targetId}/qr-label";
            var response = await client.PostAsJsonAsync(path, new GenerateQrLabelRequest("Prepare revocation test", true));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var label = await response.Content.ReadFromJsonAsync<QrLabelResponse>();
            Assert.NotNull(label);
            entityId = label.Id;
        }
        else
        {
            var response = await client.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
                $"revoke-{Guid.NewGuid():N}@example.test", "Revocation Employee", Roles.Employee, reason: "Prepare revocation test", confirmed: true));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var user = await response.Content.ReadFromJsonAsync<CreateUserResponse>();
            Assert.NotNull(user);
            targetId = user.User.Id;
            path = $"/api/v1/users/{targetId}/activation-token";
            using var scope = factory.Services.CreateScope();
            entityId = await scope.ServiceProvider.GetRequiredService<SentinelDbContext>().AccountActivationTokens
                .Where(x => x.UserId == targetId).Select(x => x.Id).SingleAsync();
        }

        foreach (var json in new string?[] { null, "null", "{\"reason\":\"Denied test\",\"confirmed\":false}", "{\"reason\":\" \",\"confirmed\":true}" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, path);
            if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var revokedAt = qr
                ? await db.DeviceQrLabels.Where(x => x.Id == entityId).Select(x => x.RevokedAt).SingleAsync()
                : await db.AccountActivationTokens.Where(x => x.Id == entityId).Select(x => x.RevokedAt).SingleAsync();
            Assert.Null(revokedAt);
            Assert.False(await db.AuditLogs.AnyAsync(x => x.Action == action));
        }

        const string reason = "Employee requested replacement";
        using var valid = new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new { reason, confirmed = true }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(valid)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var finalRevokedAt = qr
            ? await finalDb.DeviceQrLabels.Where(x => x.Id == entityId).Select(x => x.RevokedAt).SingleAsync()
            : await finalDb.AccountActivationTokens.Where(x => x.Id == entityId).Select(x => x.RevokedAt).SingleAsync();
        Assert.NotNull(finalRevokedAt);
        var audit = await finalDb.AuditLogs.SingleAsync(x => x.Action == action);
        Assert.StartsWith(reason, audit.Reason);
        Assert.Equal("Success", audit.Outcome);
        Assert.NotEqual(Guid.Empty, audit.ActorId);
    }
}
