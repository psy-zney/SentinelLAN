using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AuthenticationApiTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task DevelopmentOpenApiIncludesAuthenticationAndCsrfMetadata()
    {
        using var client = factory.CreateClient();
        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/auth/refresh", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/auth/logout", document, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(document);
        var schemes = json.RootElement.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(schemes.TryGetProperty("accessCookie", out _));
        Assert.True(schemes.TryGetProperty("refreshCookie", out _));
        Assert.True(schemes.TryGetProperty("csrfHeader", out _));
        Assert.True(schemes.TryGetProperty("agentDeviceId", out _));
        Assert.True(schemes.TryGetProperty("agentDeviceSecret", out _));
        AssertSecurityRequirement(json, "/api/v1/devices", "get", "accessCookie");
        AssertSecurityRequirement(json, "/api/v1/auth/refresh", "post", "refreshCookie", "csrfHeader");
        AssertSecurityRequirement(json, "/api/v1/agent/heartbeat", "post", "agentDeviceId", "agentDeviceSecret");
    }

    [Fact]
    public async Task LoginUsesHttpOnlyCookiesAndLogoutRevokesTheSession()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await LoginAsync(client, "admin@sentinellan.local");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        var setCookies = login.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(setCookies, value => value.StartsWith("sentinellan.access=", StringComparison.Ordinal) && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, value => value.StartsWith("sentinellan.refresh=", StringComparison.Ordinal) && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dashboard")).StatusCode);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/dashboard")).StatusCode);
    }

    [Fact]
    public async Task ReusingRotatedRefreshTokenRevokesItsReplacement()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await LoginAsync(client, "admin@sentinellan.local");
        var oldRefresh = ReadCookie(login, "sentinellan.refresh");

        var firstRefresh = await RefreshAsync(client, oldRefresh);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var replacement = ReadCookie(firstRefresh, "sentinellan.refresh");

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, oldRefresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, replacement)).StatusCode);
    }

    [Fact]
    public async Task PoliciesAndDeviceScopeBlockWrongRoleAssignmentAndTenant()
    {
        var seeded = await SeedScopedDevicesAsync();

        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(admin, "admin@sentinellan.local")).StatusCode);
        var devices = await admin.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices");
        Assert.NotNull(devices);
        Assert.Contains(devices, device => device.Id == seeded.AssignedDeviceId);
        Assert.DoesNotContain(devices, device => device.Id == seeded.OtherTenantDeviceId);

        using var technician = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(technician, "technician@sentinellan.local")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync("/api/v1/audit-logs")).StatusCode);

        using var employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employee, "employee@sentinellan.local")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync($"/api/v1/devices/{seeded.AssignedDeviceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/v1/devices/{seeded.UnassignedDeviceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/v1/devices/{seeded.OtherTenantDeviceId}")).StatusCode);
        var myDevice = await employee.GetFromJsonAsync<EmployeeDeviceDto>("/api/v1/my-device");
        Assert.NotNull(myDevice);
        Assert.Equal(seeded.AssignedDeviceId, myDevice.Device.Id);
    }

    [Fact]
    public async Task AgentPolicyRequiresSeparateHeaderCredentialAndBindsTheDevice()
    {
        var seeded = await SeedScopedDevicesAsync();
        using var client = factory.CreateClient();
        var heartbeat = new HeartbeatRequest(Guid.NewGuid().ToString("N"), 10, 20, 30, "Windows 11", "test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/agent/heartbeat", heartbeat)).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent/heartbeat") { Content = JsonContent.Create(heartbeat) };
        request.Headers.Add("X-SentinelLAN-Device-Id", seeded.AssignedDeviceId.ToString());
        request.Headers.Add("X-SentinelLAN-Device-Secret", "agent-test-secret");
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task ConfiguredDatabaseProviderUsesPostgreSqlInsteadOfTheFallback()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();

        Assert.True(await db.Database.CanConnectAsync());
        Assert.Equal(
            factory.UsesPostgreSql ? "Npgsql.EntityFrameworkCore.PostgreSQL" : "Microsoft.EntityFrameworkCore.InMemory",
            db.Database.ProviderName);
        Assert.NotEmpty((await db.Devices.FirstAsync()).RowVersion);
    }

    private async Task<(Guid AssignedDeviceId, Guid UnassignedDeviceId, Guid OtherTenantDeviceId)> SeedScopedDevicesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var demo = await db.Organizations.SingleAsync(organization => organization.Code == "demo");
        var employee = await db.Users.SingleAsync(user => user.OrganizationId == demo.Id && user.Role == Roles.Employee);
        var other = await db.Organizations.SingleOrDefaultAsync(organization => organization.Code == "other-test");
        if (other is null)
        {
            other = new Organization { Code = "other-test", Name = "Other test tenant" };
            db.Add(other);
        }

        var assigned = await db.Devices.SingleOrDefaultAsync(device => device.OrganizationId == demo.Id && device.AssignedUserId == employee.Id);
        if (assigned is null)
        {
            assigned = new Device { OrganizationId = demo.Id, AssignedUserId = employee.Id, Name = "EMPLOYEE-TEST", OsVersion = "Windows 11", AgentVersion = "test" };
            db.Add(assigned);
        }

        var credential = await db.DeviceCredentials.SingleOrDefaultAsync(item => item.DeviceId == assigned.Id);
        if (credential is null)
        {
            db.Add(new DeviceCredential
            {
                OrganizationId = demo.Id,
                DeviceId = assigned.Id,
                SecretHash = SecretHash.Create("agent-test-secret")
            });
        }

        var unassigned = await db.Devices.SingleOrDefaultAsync(device => device.Name == "UNASSIGNED-TEST");
        if (unassigned is null)
        {
            unassigned = new Device { OrganizationId = demo.Id, Name = "UNASSIGNED-TEST", OsVersion = "Windows 11", AgentVersion = "test" };
            db.Add(unassigned);
        }

        var otherTenant = await db.Devices.SingleOrDefaultAsync(device => device.Name == "OTHER-TENANT-TEST");
        if (otherTenant is null)
        {
            otherTenant = new Device { OrganizationId = other.Id, AssignedUserId = employee.Id, Name = "OTHER-TENANT-TEST", OsVersion = "Windows 11", AgentVersion = "test" };
            db.Add(otherTenant);
        }

        await db.SaveChangesAsync();
        return (assigned.Id, unassigned.Id, otherTenant.Id);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", email, "local-demo-only"));

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"sentinellan.refresh={refreshToken}");
        request.Headers.Add("X-SentinelLAN-CSRF", "1");
        return client.SendAsync(request);
    }

    private static string ReadCookie(HttpResponseMessage response, string name)
    {
        var prefix = $"{name}=";
        var value = response.Headers.GetValues("Set-Cookie").Single(header => header.StartsWith(prefix, StringComparison.Ordinal));
        return value[prefix.Length..value.IndexOf(';')];
    }

    private static void AssertSecurityRequirement(JsonDocument document, string path, string method, params string[] expectedSchemes)
    {
        var requirements = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("security");
        Assert.Contains(requirements.EnumerateArray(), requirement =>
            expectedSchemes.All(scheme => requirement.TryGetProperty(scheme, out _)));
    }
}

public sealed class SentinelApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"sentinellan-auth-tests-{Guid.NewGuid():N}";
    public bool UsesPostgreSql { get; } = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__SentinelLAN"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            if (!UsesPostgreSql)
            {
                services.RemoveAll<SentinelDbContext>();
                services.RemoveAll<DbContextOptions<SentinelDbContext>>();
                services.AddDbContext<SentinelDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }
        });
    }
}
