using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.IntegrationTests;

public sealed class ActivationAndQrIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AccountActivationLifecycleFullFlowSucceeds()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // 1. Login as Admin
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // 2. Admin creates a new user without password (returns activation token)
        var createReq = new CreateUserRequest("newemp@sentinellan.local", "New Employee", Roles.Employee, reason: "New team hire", confirmed: true);
        var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(createReq)
        };
        createMsg.Headers.Add("X-SentinelLAN-CSRF", "1");
        var createRes = await client.SendAsync(createMsg);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var createResult = await createRes.Content.ReadFromJsonAsync<CreateUserResponse>(JsonOptions);
        Assert.NotNull(createResult);
        Assert.Equal("PendingActivation", createResult.User.Status);
        Assert.False(string.IsNullOrWhiteSpace(createResult.ActivationToken));

        // 3. User attempts to login while still pending -> must be rejected (401)
        using var unauthClient = factory.CreateClient();
        var pendingLoginRes = await unauthClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "newemp@sentinellan.local", "SomePassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, pendingLoginRes.StatusCode);

        // 4. Validate activation token anonymously via POST (prevents URL query string leakage)
        var validateReq = new ValidateActivationTokenRequest(createResult.ActivationToken);
        var validateRes = await unauthClient.PostAsJsonAsync("/api/v1/auth/activation/validate", validateReq);
        Assert.Equal(HttpStatusCode.OK, validateRes.StatusCode);
        var validateResult = await validateRes.Content.ReadFromJsonAsync<ValidateActivationTokenResponse>(JsonOptions);
        Assert.NotNull(validateResult);
        Assert.True(validateResult.Valid);

        // Query-string validation is no longer supported because proxies can log the token.
        var validateGetRes = await unauthClient.GetAsync($"/api/v1/auth/activation/validate?token={createResult.ActivationToken}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, validateGetRes.StatusCode);

        // 5. Activate account with new strong password
        var activateReq = new ActivateAccountRequest(createResult.ActivationToken!, "SuperSecretPass123!");
        var activateRes = await unauthClient.PostAsJsonAsync("/api/v1/auth/activate", activateReq);
        Assert.Equal(HttpStatusCode.OK, activateRes.StatusCode);

        // 6. Token is single-use: activating again must be rejected (400)
        var replayRes = await unauthClient.PostAsJsonAsync("/api/v1/auth/activate", activateReq);
        Assert.Equal(HttpStatusCode.BadRequest, replayRes.StatusCode);

        // 7. Login with the newly activated password -> must succeed
        using var empClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var newLoginRes = await empClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "newemp@sentinellan.local", "SuperSecretPass123!"));
        Assert.Equal(HttpStatusCode.OK, newLoginRes.StatusCode);

        var sessionRes = await empClient.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, sessionRes.StatusCode);
    }

    [Fact]
    public async Task ConcurrentActivationConsumesInvitationExactlyOnce()
    {
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var email = $"concurrent-{Guid.NewGuid():N}@sentinellan.local";
        var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(new CreateUserRequest(email, "Concurrent Employee", Roles.Employee, reason: "Concurrency verification", confirmed: true))
        };
        createMessage.Headers.Add("X-SentinelLAN-CSRF", "1");
        var createResponse = await adminClient.SendAsync(createMessage);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateUserResponse>(JsonOptions);
        Assert.NotNull(created?.ActivationToken);

        var request = new ActivateAccountRequest(created.ActivationToken, "ConcurrentPassword123!");
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/auth/activate", request),
            secondClient.PostAsJsonAsync("/api/v1/auth/activate", request));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task QrLifecycleAndScopedRoutingFlowSucceeds()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Login as Technician
        var techLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "technician@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, techLogin.StatusCode);

        // Fetch demo devices
        var devicesRes = await client.GetAsync("/api/v1/devices");
        var devices = await devicesRes.Content.ReadFromJsonAsync<DeviceDto[]>(JsonOptions);
        Assert.NotNull(devices);
        Assert.NotEmpty(devices);
        var targetDevice = devices[0];

        // 1. Technician generates QR label for target device
        var genReq = new GenerateQrLabelRequest("Label replacement", true);
        var genMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/devices/{targetDevice.Id}/qr-label")
        {
            Content = JsonContent.Create(genReq)
        };
        genMsg.Headers.Add("X-SentinelLAN-CSRF", "1");
        var genRes = await client.SendAsync(genMsg);
        Assert.Equal(HttpStatusCode.Created, genRes.StatusCode);

        var genResult = await genRes.Content.ReadFromJsonAsync<QrLabelResponse>(JsonOptions);
        Assert.NotNull(genResult);
        Assert.False(string.IsNullOrWhiteSpace(genResult.Code));

        // 2. Anonymous public resolve: protects privacy (no full serial, no internal UUID, no assigned user)
        using var unauthClient = factory.CreateClient();
        var publicRes = await unauthClient.GetAsync($"/api/v1/qr/{genResult.Code}/public");
        Assert.Equal(HttpStatusCode.OK, publicRes.StatusCode);
        var publicData = await publicRes.Content.ReadFromJsonAsync<PublicQrResolveDto>(JsonOptions);
        Assert.NotNull(publicData);
        Assert.Equal(targetDevice.Name, publicData.DeviceName);

        // 3. Authenticated resolve: Technician routed to /devices/{deviceId}
        var authResolveRes = await client.GetAsync($"/api/v1/qr/{genResult.Code}");
        Assert.Equal(HttpStatusCode.OK, authResolveRes.StatusCode);
        var authResolveData = await authResolveRes.Content.ReadFromJsonAsync<AuthenticatedQrResolveDto>(JsonOptions);
        Assert.NotNull(authResolveData);
        Assert.Equal($"/devices/{targetDevice.Id}", authResolveData.NextRoute);

        // 4. Revoke QR label
        var revokeMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/devices/{targetDevice.Id}/qr-label")
        {
            Content = JsonContent.Create(new RevokeQrLabelRequest("Damaged sticker", true))
        };
        revokeMsg.Headers.Add("X-SentinelLAN-CSRF", "1");
        var revokeRes = await client.SendAsync(revokeMsg);
        Assert.Equal(HttpStatusCode.NoContent, revokeRes.StatusCode);

        // 5. Old QR code is now revoked -> public resolve returns 404
        var revokedPublicRes = await unauthClient.GetAsync($"/api/v1/qr/{genResult.Code}/public");
        Assert.Equal(HttpStatusCode.NotFound, revokedPublicRes.StatusCode);
    }

    [Fact]
    public async Task MyDeviceAndIncidentReportingScopedToAssignedEmployee()
    {
        using var empClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Login as Demo Employee (seeded with assigned device)
        var login = await empClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // 1. GET /api/v1/my-device returns assigned device with privacy manifest
        var myDeviceRes = await empClient.GetAsync("/api/v1/my-device");
        Assert.Equal(HttpStatusCode.OK, myDeviceRes.StatusCode);

        var myDevice = await myDeviceRes.Content.ReadFromJsonAsync<MyDeviceDto>(JsonOptions);
        Assert.NotNull(myDevice);
        Assert.NotNull(myDevice.PrivacyManifest);
        Assert.NotEmpty(myDevice.PrivacyManifest.CollectedTechnicalData);
        Assert.NotEmpty(myDevice.PrivacyManifest.StrictlyProhibitedData);

        // 2. GET /api/v1/my-device/telemetry
        var telemetryRes = await empClient.GetAsync("/api/v1/my-device/telemetry?limit=5");
        Assert.Equal(HttpStatusCode.OK, telemetryRes.StatusCode);

        // 3. POST /api/v1/my-device/incidents reports incident for employee's assigned device
        var reportReq = new ReportMyDeviceIncidentRequest("Display flickers on dock disconnect", "Tested on two monitors", "Medium", "employee-incident-integration-1");
        var reportMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/my-device/incidents")
        {
            Content = JsonContent.Create(reportReq)
        };
        reportMsg.Headers.Add("X-SentinelLAN-CSRF", "1");
        reportMsg.Headers.Add("Idempotency-Key", "employee-incident-integration-1");
        var reportRes = await empClient.SendAsync(reportMsg);
        Assert.Equal(HttpStatusCode.Created, reportRes.StatusCode);

        var reportResult = await reportRes.Content.ReadFromJsonAsync<IncidentDto>(JsonOptions);
        Assert.NotNull(reportResult);
        Assert.NotEqual(Guid.Empty, reportResult.Id);
        Assert.Equal("Display flickers on dock disconnect", reportResult.Title);

        using var replayMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/my-device/incidents")
        {
            Content = JsonContent.Create(reportReq)
        };
        replayMsg.Headers.Add("X-SentinelLAN-CSRF", "1");
        replayMsg.Headers.Add("Idempotency-Key", "employee-incident-integration-1");
        var replayRes = await empClient.SendAsync(replayMsg);
        Assert.Equal(HttpStatusCode.Created, replayRes.StatusCode);
        var replayResult = await replayRes.Content.ReadFromJsonAsync<IncidentDto>(JsonOptions);
        Assert.NotNull(replayResult);
        Assert.Equal(reportResult.Id, replayResult.Id);

        using var conflictMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/my-device/incidents")
        {
            Content = JsonContent.Create(reportReq with { Title = "Different incident" })
        };
        conflictMsg.Headers.Add("X-SentinelLAN-CSRF", "1");
        conflictMsg.Headers.Add("Idempotency-Key", "employee-incident-integration-1");
        var conflictRes = await empClient.SendAsync(conflictMsg);
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);

        // 4. GET /api/v1/my-device/incidents includes the reported incident
        var listIncRes = await empClient.GetAsync("/api/v1/my-device/incidents");
        Assert.Equal(HttpStatusCode.OK, listIncRes.StatusCode);
        var incidents = await listIncRes.Content.ReadFromJsonAsync<IncidentDto[]>(JsonOptions);
        Assert.NotNull(incidents);
        Assert.Contains(incidents, inc => inc.Title == "Display flickers on dock disconnect");
    }

    [Fact]
    public async Task AnonymousAccessToItamRoutesReturnsUnauthorized()
    {
        using var unauthClient = factory.CreateClient();
        var randomId = Guid.NewGuid();

        var routes = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, $"/api/v1/devices/{randomId}/asset-detail"),
            (HttpMethod.Get, $"/api/v1/devices/{randomId}/timeline"),
            (HttpMethod.Get, "/api/v1/incidents"),
            (HttpMethod.Post, "/api/v1/incidents"),
            (HttpMethod.Get, "/api/v1/work-orders")
        };

        foreach (var (method, path) in routes)
        {
            using var req = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post)
            {
                req.Content = JsonContent.Create(new { DeviceId = randomId, Title = "Test Incident", Severity = "Low" });
            }
            var res = await unauthClient.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await unauthClient.GetAsync($"/api/v1/public/qr/{randomId}")).StatusCode);
    }
}
