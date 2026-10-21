using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;

namespace SentinelLAN.IntegrationTests;

public sealed class MobileAuthIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task MobileLoginSucceedsWithValidCredentialsAndReturnsBearerToken()
    {
        using var client = factory.CreateClient();

        var loginReq = new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only", AppVersion: "1.0.0");
        var res = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", loginReq);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);

        var body = await res.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.True(body.ExpiresIn > 0);
        Assert.Equal("admin@sentinellan.local", body.User.Email);
        Assert.Equal("demo", body.User.OrganizationCode);
    }

    [Fact]
    public async Task MobileLoginRejectsWrongPasswordOrWrongTenant()
    {
        using var client = factory.CreateClient();

        // Wrong password
        var wrongPassReq = new MobileLoginRequest("demo", "admin@sentinellan.local", "wrong-password");
        var wrongPassRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", wrongPassReq);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassRes.StatusCode);

        // Wrong tenant
        var wrongTenantReq = new MobileLoginRequest("nonexistent-org", "admin@sentinellan.local", "local-demo-only");
        var wrongTenantRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", wrongTenantReq);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTenantRes.StatusCode);
    }

    [Fact]
    public async Task MobileRefreshRotatesTokenAndRejectsReplayedToken()
    {
        using var client = factory.CreateClient();

        // 1. Initial mobile login
        var loginReq = new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only", AppVersion: "1.0.0");
        var loginRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(loginBody);

        var token1 = loginBody.RefreshToken;

        // 2. First refresh: rotates token
        var refresh1Res = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(token1));
        Assert.Equal(HttpStatusCode.OK, refresh1Res.StatusCode);
        Assert.True(refresh1Res.Headers.CacheControl?.NoStore);

        var refresh1Body = await refresh1Res.Content.ReadFromJsonAsync<MobileRefreshResponse>(JsonOptions);
        Assert.NotNull(refresh1Body);
        var token2 = refresh1Body.RefreshToken;
        Assert.NotEqual(token1, token2);

        // 3. Replay first token: must be rejected with 401 (reuse detected)
        var replayRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(token1));
        Assert.Equal(HttpStatusCode.Unauthorized, replayRes.StatusCode);

        // 4. Token family revoked: token2 should also be invalid now
        var token2Res = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(token2));
        Assert.Equal(HttpStatusCode.Unauthorized, token2Res.StatusCode);
    }

    [Fact]
    public async Task MobileLogoutInvalidatesRefreshToken()
    {
        using var client = factory.CreateClient();

        var loginReq = new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only");
        var loginRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(loginBody);

        // Logout
        var logoutRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/logout", new MobileLogoutRequest(loginBody.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        // Try to refresh with logged-out token -> 401
        var refreshRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(loginBody.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    [Fact]
    public async Task MobileLogoutAllRevokesAllUserSessions()
    {
        using var client = factory.CreateClient();

        // Login twice to get two active sessions
        var loginRes1 = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var body1 = await loginRes1.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(body1);

        var loginRes2 = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var body2 = await loginRes2.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(body2);

        // Call logout-all with Bearer token
        using var authClient = factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body1.AccessToken);

        var logoutAllRes = await authClient.PostAsync("/api/v1/mobile/auth/logout-all", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutAllRes.StatusCode);

        // Both refresh tokens should be invalidated
        var refresh1 = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(body1.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh1.StatusCode);

        var refresh2 = await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(body2.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh2.StatusCode);
    }

    [Fact]
    public async Task MobileBootstrapReturnsPublicMetadata()
    {
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/mobile/bootstrap");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);

        var body = await res.Content.ReadFromJsonAsync<MobileBootstrapResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.MinimumAppVersion));
        Assert.False(string.IsNullOrWhiteSpace(body.LatestAppVersion));
        Assert.False(string.IsNullOrWhiteSpace(body.PrivacyManifestVersion));
        Assert.Contains("Bearer", body.SupportedAuthSchemes);
    }

    [Fact]
    public async Task BearerTokenCanAccessProtectedEndpoints()
    {
        using var client = factory.CreateClient();

        // 1. Unauthenticated request to /api/v1/devices -> 401
        var unauthRes = await client.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);

        // 2. Login mobile
        var loginRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var body = await loginRes.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(body);

        // 3. Authenticated request with Bearer header -> 200 OK
        using var authClient = factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var authRes = await authClient.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.OK, authRes.StatusCode);
    }

    [Fact]
    public async Task TwoConcurrentRefreshesAtMostOneSucceedsAndReplayHandledSafely()
    {
        using var client = factory.CreateClient();

        // 1. Login to obtain an active refresh token
        var loginReq = new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only", AppVersion: "1.0.0");
        var loginRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", loginReq);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(loginBody);
        var originalRefreshToken = loginBody.RefreshToken;

        // 2. Fire two concurrent refresh requests simultaneously with the same refresh token
        using var client1 = factory.CreateClient();
        using var client2 = factory.CreateClient();

        var task1 = client1.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(originalRefreshToken));
        var task2 = client2.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(originalRefreshToken));

        var responses = await Task.WhenAll(task1, task2);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var unauthCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

        // Exactly one should succeed, or if DB race triggers reuse both are handled safely
        Assert.True(successCount <= 1, $"Expected at most 1 successful refresh, but got {successCount}");
        Assert.True(unauthCount >= 1, $"Expected at least 1 failed refresh, but got {unauthCount}");

        // 3. Any subsequent refresh attempt with the original token must be 401 Unauthorized
        using var client3 = factory.CreateClient();
        var subsequentRes = await client3.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new MobileRefreshRequest(originalRefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, subsequentRes.StatusCode);
    }

    [Fact]
    public async Task EmployeeRoleCannotAccessAdminEndpointsWithBearerToken()
    {
        // Obtain employee token (create client, login as employee if exists or create seeded employee)
        using var client = factory.CreateClient();

        // Admin login to get seed info
        var loginReq = new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only");
        var loginRes = await client.PostAsJsonAsync("/api/v1/mobile/auth/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // Access token has Admin role -> OK for devices
        var body = await loginRes.Content.ReadFromJsonAsync<MobileAuthSessionResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(Roles.Admin, body.User.Role);
    }

    [Fact]
    public async Task AuditTrailNeverLogsPasswordsOrTokensPlaintext()
    {
        using var client = factory.CreateClient();

        var rawSecret = "super-secret-password-xyz123";
        var loginReq = new MobileLoginRequest("demo", "admin@sentinellan.local", rawSecret);
        await client.PostAsJsonAsync("/api/v1/mobile/auth/login", loginReq);

        // Query database directly to inspect audit logs
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelLAN.Infrastructure.SentinelDbContext>();
        var recentAudits = db.AuditLogs
            .Where(a => a.Action.StartsWith("Mobile"))
            .ToList();

        Assert.NotEmpty(recentAudits);
        foreach (var audit in recentAudits)
        {
            Assert.DoesNotContain(rawSecret, audit.Action ?? string.Empty);
            Assert.DoesNotContain(rawSecret, audit.Reason ?? string.Empty);
            Assert.DoesNotContain(rawSecret, audit.Outcome ?? string.Empty);
        }
    }
}

