using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelLAN.Api;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class SelfHostDeploymentTests
{
    [Theory]
    [InlineData("/api/v1/platform/auth/login")]
    [InlineData("/api/v1/platform/companies")]
    [InlineData("/api/v1/platform/system/status")]
    [InlineData("/api/v1/platform/vps-nodes")]
    public async Task SelfHostDoesNotExposePlatformRoutes(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(path, new { email = "owner@example.test", password = "irrelevant" })).StatusCode);
    }

    [Fact]
    public async Task CompanyLoginWorksWithoutCreatingOrRegisteringPlatformOwner()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/devices")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.False(await db.Users.AnyAsync(user => user.Role == "PlatformOwner"));
        Assert.False(await db.Organizations.AnyAsync(org => org.Code == "_platform"));
        var openApi = await client.GetStringAsync("/openapi/v1.json");
        Assert.DoesNotContain("/api/v1/platform/", openApi, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompanyTokenCarriesNoDestinationAndIsConsumedOnlyOnce()
    {
        await using var factory = CreateFactory();
        using var admin = factory.CreateClient();
        await admin.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        using var mint = new HttpRequestMessage(HttpMethod.Post, "/api/v1/enrollment-tokens")
        {
            Content = JsonContent.Create(new EnrollmentTokenRequest(15, "Company enrollment test", true))
        };
        mint.Headers.Add("X-SentinelLAN-CSRF", "1");
        var response = await admin.SendAsync(mint);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issued = (await response.Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!;
        Assert.Equal(issued.Token, issued.ConnectionCode);
        Assert.Equal(64, issued.ConnectionCode!.Length);
        Assert.All(issued.ConnectionCode, c => Assert.True(char.IsAsciiHexDigit(c)));
        using var agent = factory.CreateClient();
        var enrollment = new EnrollRequest(issued.Token, "SELFHOST-LAB", "Windows", "test", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsJsonAsync("/api/v1/agent/enroll", enrollment)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await agent.PostAsJsonAsync("/api/v1/agent/enroll", enrollment with { DeviceName = "OTHER-LAB" })).StatusCode);
    }

    [Theory]
    [InlineData("SelfHots")]
    [InlineData("Platform")]
    public void UnsupportedModeFailsRatherThanEnablingPlatform(string mode)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SENTINELLAN_DEPLOYMENT_MODE"] = mode
        }).Build();
        Assert.Throws<InvalidOperationException>(() => DeploymentSettings.FromConfiguration(config));
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseName = $"selfhost-{Guid.NewGuid():N}";
        using var signingKey = RSA.Create(3072);
        var privateKey = signingKey.ExportPkcs8PrivateKeyPem();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("SENTINELLAN_DEPLOYMENT_MODE", "SelfHost");
            builder.UseSetting("SENTINELLAN_COMMAND_PRIVATE_KEY_PEM", privateKey);
            builder.UseSetting("SENTINELLAN_COMMAND_KEY_ID", "test-company-key");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<SentinelDbContext>();
                services.RemoveAll<DbContextOptions<SentinelDbContext>>();
                services.AddDbContext<SentinelDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }
}
