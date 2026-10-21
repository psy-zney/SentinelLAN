using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class DeviceLifecycleIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task EnrollmentSucceedsAndAuditsSuccess()
    {
        var rawToken = $"enroll-valid-{Guid.NewGuid():N}";
        var organizationId = await SeedEnrollmentTokenAsync(rawToken, DateTimeOffset.UtcNow.AddHours(1));

        using var client = factory.CreateClient();
        var enrollRequest = new EnrollRequest(rawToken, "TEST-WORKSTATION-01", "Windows 11 Enterprise", "0.1.0");
        var response = await client.PostAsJsonAsync("/api/v1/agent/enroll", enrollRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrolled = await response.Content.ReadFromJsonAsync<EnrollResponse>();
        Assert.NotNull(enrolled);
        Assert.NotEqual(Guid.Empty, enrolled.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(enrolled.DeviceSecret));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();

        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == enrolled.DeviceId);
        Assert.NotNull(device);
        Assert.Equal(organizationId, device.OrganizationId);
        Assert.Equal("TEST-WORKSTATION-01", device.Name);

        var credential = await db.DeviceCredentials.SingleOrDefaultAsync(c => c.DeviceId == enrolled.DeviceId);
        Assert.NotNull(credential);
        Assert.True(SecretHash.Matches(enrolled.DeviceSecret, credential.SecretHash));

        var audit = await db.AuditLogs.SingleOrDefaultAsync(a =>
            a.OrganizationId == organizationId &&
            a.DeviceId == enrolled.DeviceId &&
            a.Action == "AgentEnrolled" &&
            a.Outcome == "Success");
        Assert.NotNull(audit);
        Assert.Equal(enrolled.DeviceId, audit.ActorId);
    }

    [Fact]
    public async Task EnrollmentRejectsReusedTokenAndAuditsFailure()
    {
        var rawToken = $"enroll-reuse-{Guid.NewGuid():N}";
        var organizationId = await SeedEnrollmentTokenAsync(rawToken, DateTimeOffset.UtcNow.AddHours(1));

        using var client = factory.CreateClient();
        var firstRequest = new EnrollRequest(rawToken, "PC-FIRST", "Windows 11", "0.1.0");
        var firstResponse = await client.PostAsJsonAsync("/api/v1/agent/enroll", firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Attempt to reuse the consumed token
        var reuseRequest = new EnrollRequest(rawToken, "PC-REUSE-ATTEMPT", "Windows 11", "0.1.0");
        var secondResponse = await client.PostAsJsonAsync("/api/v1/agent/enroll", reuseRequest);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();

        var failureAudit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.OrganizationId == organizationId &&
            a.Action == "AgentEnrollmentFailed" &&
            a.Reason == "Token already used" &&
            a.Outcome == "Failed");
        Assert.NotNull(failureAudit);
    }

    [Fact]
    public async Task EnrollmentRejectsExpiredTokenAndAuditsFailure()
    {
        var rawToken = $"enroll-expired-{Guid.NewGuid():N}";
        var organizationId = await SeedEnrollmentTokenAsync(rawToken, DateTimeOffset.UtcNow.AddMinutes(-5));

        using var client = factory.CreateClient();
        var request = new EnrollRequest(rawToken, "PC-EXPIRED", "Windows 11", "0.1.0");
        var response = await client.PostAsJsonAsync("/api/v1/agent/enroll", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();

        var failureAudit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.OrganizationId == organizationId &&
            a.Action == "AgentEnrollmentFailed" &&
            a.Reason == "Token expired" &&
            a.Outcome == "Failed");
        Assert.NotNull(failureAudit);
    }

    [Fact]
    public async Task HeartbeatIdempotencyPreventsDuplicateSnapshots()
    {
        var rawToken = $"enroll-hb-{Guid.NewGuid():N}";
        var organizationId = await SeedEnrollmentTokenAsync(rawToken, DateTimeOffset.UtcNow.AddHours(1));

        using var client = factory.CreateClient();
        var enrollResponse = await client.PostAsJsonAsync("/api/v1/agent/enroll", new EnrollRequest(rawToken, "PC-HEARTBEAT", "Windows 11", "0.1.0"));
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);
        var identity = (await enrollResponse.Content.ReadFromJsonAsync<EnrollResponse>())!;

        var idempotencyKey = $"idempotency-{Guid.NewGuid():N}";
        var heartbeat = new HeartbeatRequest(idempotencyKey, 34.5, 56.2, 78.1, "Windows 11", "0.1.0");

        // First heartbeat delivery
        using var firstRequest = CreateAgentRequest(HttpMethod.Post, "/api/v1/agent/heartbeat", identity.DeviceId, identity.DeviceSecret, heartbeat);
        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);

        // Duplicate delivery with the exact same idempotency key
        using var duplicateRequest = CreateAgentRequest(HttpMethod.Post, "/api/v1/agent/heartbeat", identity.DeviceId, identity.DeviceSecret, heartbeat);
        var duplicateResponse = await client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();

        var heartbeatCount = await db.Heartbeats.CountAsync(h =>
            h.DeviceId == identity.DeviceId &&
            h.OrganizationId == organizationId &&
            h.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, heartbeatCount);

        var telemetryCount = await db.Telemetry.CountAsync(t =>
            t.DeviceId == identity.DeviceId &&
            t.OrganizationId == organizationId);
        Assert.Equal(1, telemetryCount);

        var device = await db.Devices.SingleAsync(d => d.Id == identity.DeviceId);
        Assert.True(device.IsOnline(DateTimeOffset.UtcNow));
        Assert.NotNull(device.LastSeenAt);
    }

    [Fact]
    public async Task TenantIsolationEnforcedForDevicesAndTelemetry()
    {
        var setup = await SeedTwoTenantsAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Login as Tenant A Admin
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(setup.TenantACode, setup.TenantAAdminEmail, "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // List devices: must include Tenant A's device and not Tenant B's
        var devices = await client.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices");
        Assert.NotNull(devices);
        Assert.Contains(devices, d => d.Id == setup.DeviceAId);
        Assert.DoesNotContain(devices, d => d.Id == setup.DeviceBId);

        // Direct device detail of Tenant B: must return 404 NotFound
        var foreignDevice = await client.GetAsync($"/api/v1/devices/{setup.DeviceBId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDevice.StatusCode);

        // Direct telemetry of Tenant B: must return 404 NotFound
        var foreignTelemetry = await client.GetAsync($"/api/v1/devices/{setup.DeviceBId}/telemetry");
        Assert.Equal(HttpStatusCode.NotFound, foreignTelemetry.StatusCode);

        // Telemetry of Tenant A: returns valid DTO array
        var ownTelemetry = await client.GetFromJsonAsync<TelemetrySnapshotDto[]>($"/api/v1/devices/{setup.DeviceAId}/telemetry");
        Assert.NotNull(ownTelemetry);
        Assert.NotEmpty(ownTelemetry);
        Assert.Equal(setup.DeviceAId, ownTelemetry[0].DeviceId);

        // Agent from Tenant A attempting to send heartbeat for Tenant B device is rejected
        using var spoofedHeartbeat = CreateAgentRequest(
            HttpMethod.Post,
            "/api/v1/agent/heartbeat",
            setup.DeviceBId,
            setup.DeviceASecret,
            new HeartbeatRequest(Guid.NewGuid().ToString("N"), 10, 10, 10, "Windows 11", "0.1.0"));
        using var unauthClient = factory.CreateClient();
        var spoofResponse = await unauthClient.SendAsync(spoofedHeartbeat);
        Assert.Equal(HttpStatusCode.Unauthorized, spoofResponse.StatusCode);
    }

    private async Task<Guid> SeedEnrollmentTokenAsync(string rawToken, DateTimeOffset expiresAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var org = await db.Organizations.FirstAsync(o => o.Code == "demo");

        var token = new DeviceEnrollmentToken
        {
            OrganizationId = org.Id,
            TokenHash = SecretHash.Create(rawToken),
            ExpiresAt = expiresAt
        };
        db.Add(token);
        await db.SaveChangesAsync();
        return org.Id;
    }

    private async Task<(string TenantACode, string TenantAAdminEmail, Guid DeviceAId, string DeviceASecret, Guid DeviceBId)> SeedTwoTenantsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenantA = await db.Organizations.FirstAsync(o => o.Code == "demo");
        var adminA = await db.Users.FirstAsync(u => u.OrganizationId == tenantA.Id && u.Role == Roles.Admin);

        var tenantB = await db.Organizations.SingleOrDefaultAsync(o => o.Code == "tenant-b-iso");
        if (tenantB is null)
        {
            tenantB = new Organization { Code = "tenant-b-iso", Name = "Tenant B Isolation" };
            db.Add(tenantB);
            db.Add(new User
            {
                OrganizationId = tenantB.Id,
                Email = "admin@tenant-b.local",
                DisplayName = "Tenant B Admin",
                Role = Roles.Admin,
                PasswordHash = passwordHasher.Hash("local-demo-only")
            });
        }

        var secretA = "secret-a-device";
        var deviceA = new Device { OrganizationId = tenantA.Id, Name = "DEVICE-TENANT-A", OsVersion = "Windows 11", AgentVersion = "0.1.0" };
        db.Add(deviceA);
        db.Add(new DeviceCredential { OrganizationId = tenantA.Id, DeviceId = deviceA.Id, SecretHash = SecretHash.Create(secretA) });
        db.Add(new TelemetrySnapshot { OrganizationId = tenantA.Id, DeviceId = deviceA.Id, CpuPercent = 12.0, RamPercent = 34.0, DiskPercent = 56.0 });

        var secretB = "secret-b-device";
        var deviceB = new Device { OrganizationId = tenantB.Id, Name = "DEVICE-TENANT-B", OsVersion = "Windows 11", AgentVersion = "0.1.0" };
        db.Add(deviceB);
        db.Add(new DeviceCredential { OrganizationId = tenantB.Id, DeviceId = deviceB.Id, SecretHash = SecretHash.Create(secretB) });
        db.Add(new TelemetrySnapshot { OrganizationId = tenantB.Id, DeviceId = deviceB.Id, CpuPercent = 90.0, RamPercent = 85.0, DiskPercent = 70.0 });

        await db.SaveChangesAsync();
        return (tenantA.Code, adminA.Email, deviceA.Id, secretA, deviceB.Id);
    }

    private static HttpRequestMessage CreateAgentRequest(HttpMethod method, string path, Guid deviceId, string secret, object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-SentinelLAN-Device-Id", deviceId.ToString());
        request.Headers.Add("X-SentinelLAN-Device-Secret", secret);
        return request;
    }
}
