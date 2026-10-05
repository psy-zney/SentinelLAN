using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Enrollment;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class EnrollmentConnectionCodeTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task IssuedCodeUsesConfiguredServerAndCanOnlyEnrollOnceEvenBeforeExpiry()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["SENTINELLAN_AGENT_PUBLIC_URL"] = "https://sentinel.company.test" })));
        using var admin = configured.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/enrollment-tokens")
        {
            Content = JsonContent.Create(new EnrollmentTokenRequest(60, "Install authorized company device", true))
        };
        request.Headers.Add("X-SentinelLAN-CSRF", "1");
        request.Headers.Add("X-Forwarded-Host", "attacker.test");
        var response = await admin.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var issued = (await response.Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!;
        var code = EnrollmentConnectionCode.Decode(issued.ConnectionCode!);
        Assert.Equal("https://sentinel.company.test", code.ServerUrl);
        Assert.Equal(issued.Token, code.Token);
        Assert.True(code.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(50));
        using var agent = configured.CreateClient();
        var enrollment = new EnrollRequest(code.Token, $"CODE-{Guid.NewGuid():N}", "Windows", "test");
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsJsonAsync("/api/v1/agent/enroll", enrollment)).StatusCode);
        var second = await agent.PostAsJsonAsync("/api/v1/agent/enroll", enrollment with { DeviceName = "Other device" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("TokenUsed", problem.RootElement.GetProperty("code").GetString());

        await using var scope = configured.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var stored = await db.EnrollmentTokens.SingleAsync(token => token.TokenHash == SecretHash.Create(code.Token));
        Assert.NotNull(stored.UsedAt);
        var audits = await db.AuditLogs.Where(a => a.OrganizationId == stored.OrganizationId).ToListAsync();
        Assert.DoesNotContain(audits, a => a.Reason.Contains(code.Token, StringComparison.Ordinal) || a.Reason.Contains(issued.ConnectionCode!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidServerConfigurationDoesNotMintToken()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["SENTINELLAN_AGENT_PUBLIC_URL"] = "http://192.168.1.10" })));
        using var admin = configured.CreateClient();
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/enrollment-tokens")
        {
            Content = JsonContent.Create(new EnrollmentTokenRequest(15, "Must not mint invalid bootstrap", true))
        };
        request.Headers.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.SendAsync(request)).StatusCode);
        await using var scope = configured.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "EnrollmentTokenCreated" && a.Reason.Contains("Must not mint invalid bootstrap")));
    }

    [Theory]
    [InlineData("http://192.168.1.10")]
    [InlineData("https://user:password@company.test")]
    [InlineData("https://company.test/?token=hidden")]
    [InlineData("https://company.test/#fragment")]
    [InlineData("https://company.test/api")]
    [InlineData("file:///C:/temp")]
    public void CodeRejectsUnsafeDestinations(string server)
    {
        var code = new EnrollmentConnectionCode(server, new string('A', 64), DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.Throws<FormatException>(() => code.Encode());
        var untrusted = "SL1." + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(code, JsonOptions)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Throws<FormatException>(() => EnrollmentConnectionCode.Decode(untrusted));
    }

    [Theory]
    [InlineData("raw-token")]
    [InlineData("SL2.abc")]
    [InlineData("SL1.")]
    [InlineData("SL1.!@#")]
    [InlineData("SL1.bnVsbA")]
    [InlineData("SL1.e30")]
    public void CodeRejectsInvalidOrUnknownFormats(string code) =>
        Assert.Throws<FormatException>(() => EnrollmentConnectionCode.Decode(code));
}
