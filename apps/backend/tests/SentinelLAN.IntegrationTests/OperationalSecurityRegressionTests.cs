using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Api;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class OperationalSecurityRegressionTests
{
    [Fact]
    public async Task UpdatingCompanyAccountDoesNotDisconnectAnotherTenantsRealtimeClient()
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var other = new Organization { Code = "realtime-other", Name = "Realtime Other" };
        db.Add(other);
        db.Add(new User
        {
            OrganizationId = other.Id,
            Role = Roles.Admin,
            Email = "tech@example.test",
            DisplayName = "Other technician",
            PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("dummy-other-password")
        });
        await db.SaveChangesAsync();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(other.Code, "tech@example.test", "dummy-other-password"));
        var sockets = factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {Cookie(login, "sentinellan.access")}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var socket = await sockets.ConnectAsync(new Uri("ws://localhost/hubs/updates"), timeout.Token);
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e")), WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[4096];
        await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        var adminLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Cookie(adminLogin, "sentinellan.access"));
        var target = await db.Users.SingleAsync(u => u.Email == "employee@sentinellan.local");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/users/{target.Id}/status",
            new SetUserStatusRequest(UserStatuses.Locked, "Different tenant account update", true))).StatusCode);
        await factory.Services.GetRequiredService<IHubContext<UpdatesHub>>().Clients.Group(TenantGroup.Name(other.Id)).SendAsync("test-health", new { online = true }, timeout.Token);
        var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        Assert.Equal(WebSocketMessageType.Text, received.MessageType);
        Assert.Contains("test-health", Encoding.UTF8.GetString(buffer, 0, received.Count), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasswordReactivationRevokesWebAndMobileRefreshTokensAndAccessTokens()
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var admin = await db.Users.FirstAsync(u => u.Role == Roles.Admin);
        var users = scope.ServiceProvider.GetRequiredService<UserManagementService>();
        var created = await users.CreateAsync(new(admin.Id, admin.OrganizationId, Roles.Admin),
            new CreateUserRequest($"reset-{Guid.NewGuid():N}@example.test", "Reset user", Roles.Employee,
                "Recovery regression", true, "initial-password-123"), scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), default);
        var email = created.User!.Email;
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", email, "initial-password-123"));
        var oldRefresh = Cookie(login, "sentinellan.refresh");
        var oldAccess = Cookie(login, "sentinellan.access");
        var mobile = (await client.PostAsJsonAsync("/api/v1/mobile/auth/login",
            new MobileLoginRequest("demo", email, "initial-password-123"))).Content;
        var mobileSession = (await mobile.ReadFromJsonAsync<MobileAuthSessionResponse>())!;
        var issued = await users.ReissueActivationTokenAsync(new(admin.Id, admin.OrganizationId, Roles.Admin), created.User.Id,
            new ReissueActivationTokenRequest("Recover compromised password", true), default);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/activate",
            new ActivateAccountRequest(issued.Response!.ActivationToken, "replacement-password-123"))).StatusCode);

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("Cookie", $"sentinellan.refresh={oldRefresh}");
        refresh.Headers.Add("X-SentinelLAN-CSRF", "1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(refresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh",
            new MobileRefreshRequest(mobileSession.RefreshToken))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldAccess);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", email, "replacement-password-123"))).StatusCode);
    }

    [Fact]
    public async Task LoginCaseVariantsShareTheSameRateLimit()
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 30; i++)
        {
            var path = i % 2 == 0 ? "/api/v1/auth/login" : "/API/V1/AUTH/LOGIN";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path,
                new LoginRequest("demo", "missing@example.test", "invalid"))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/Api/V1/Auth/Login",
            new LoginRequest("demo", "missing@example.test", "invalid"))).StatusCode);
    }

    [Fact]
    public async Task LockingAccountClosesAnAlreadyConnectedRealtimeClient()
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await using var setupScope = factory.Services.CreateAsyncScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var demo = await setupDb.Organizations.SingleAsync(o => o.Code == "demo");
        setupDb.Add(new User { OrganizationId = demo.Id, Email = "realtime-admin@example.test", DisplayName = "Realtime Admin", Role = Roles.Admin,
            PasswordHash = setupScope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("dummy-realtime-password") });
        await setupDb.SaveChangesAsync();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "realtime-admin@example.test", "dummy-realtime-password"));
        var access = Cookie(login, "sentinellan.access");
        var sockets = factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {access}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var socket = await sockets.ConnectAsync(new Uri("ws://localhost/hubs/updates"), timeout.Token);
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e")), WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[4096];
        await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var target = await db.Users.SingleAsync(u => u.Email == "realtime-admin@example.test");
        var adminLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Cookie(adminLogin, "sentinellan.access"));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/users/{target.Id}/status",
            new SetUserStatusRequest(UserStatuses.Locked, "Realtime revocation regression", true))).StatusCode);
        var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        Assert.True(received.MessageType == WebSocketMessageType.Close || Encoding.UTF8.GetString(buffer, 0, received.Count).Contains("\"type\":7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfflineTelemetryUsesCollectionTimeAndRejectsImpossibleTimes()
    {
        await using var factory = new SentinelApiFactory();
        using var agent = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var org = await db.Organizations.SingleAsync(o => o.Code == "demo");
        var device = new Device { OrganizationId = org.Id, Name = "Timestamp test", OsVersion = "Test", AgentVersion = "Test" };
        db.Add(device);
        db.Add(new DeviceCredential { OrganizationId = org.Id, DeviceId = device.Id, SecretHash = SecretHash.Create("dummy-telemetry-secret") });
        await db.SaveChangesAsync();
        agent.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Id", device.Id.ToString());
        agent.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Secret", "dummy-telemetry-secret");
        var captured = DateTimeOffset.UtcNow.AddMinutes(-40);
        var request = new HeartbeatRequest("offline-sample", 95, 20, 30, "Test", "Test", CollectedAt: captured);
        Assert.Equal(HttpStatusCode.Accepted, (await agent.PostAsJsonAsync("/api/v1/agent/heartbeat", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsJsonAsync("/api/v1/agent/heartbeat", request)).StatusCode);
        var sample = await db.Telemetry.AsNoTracking().SingleAsync(t => t.DeviceId == device.Id);
        Assert.True((sample.CollectedAt!.Value - captured).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.True(sample.CreatedAt > captured.AddMinutes(30));
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/v1/agent/heartbeat", request with { IdempotencyKey = "future", CollectedAt = DateTimeOffset.UtcNow.AddHours(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/v1/agent/heartbeat", request with { IdempotencyKey = "too-old", CollectedAt = DateTimeOffset.UtcNow.AddDays(-1) })).StatusCode);
    }

    [Fact]
    public async Task PreviouslyDeliveredCommandAcceptsALateReceiptExactlyOnce()
    {
        await using var factory = new SentinelApiFactory();
        using var agent = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var org = await db.Organizations.SingleAsync(o => o.Code == "demo");
        var device = new Device { OrganizationId = org.Id, Name = "Receipt test", OsVersion = "Test", AgentVersion = "Test" };
        db.Add(device);
        db.Add(new DeviceCredential { OrganizationId = org.Id, DeviceId = device.Id, SecretHash = SecretHash.Create("dummy-receipt-secret") });
        var command = new DeviceCommand
        {
            OrganizationId = org.Id,
            DeviceId = device.Id,
            IssuedByUserId = Guid.NewGuid(),
            Type = "SimulateLock",
            Reason = "Receipt regression",
            Nonce = Guid.NewGuid().ToString("N"),
            Signature = "Previously delivered fixture",
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            Status = DeviceCommandStatus.Delivered
        };
        db.Add(command);
        await db.SaveChangesAsync();
        agent.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Id", device.Id.ToString());
        agent.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Secret", "dummy-receipt-secret");
        await agent.PostAsync("/api/v1/agent/commands/poll", null);
        db.ChangeTracker.Clear();
        Assert.Equal(DeviceCommandStatus.ExecutionUnconfirmed, (await db.Commands.SingleAsync(c => c.Id == command.Id)).Status);
        var path = $"/api/v1/agent/commands/{command.Id}/result";
        Assert.Equal(HttpStatusCode.Accepted, (await agent.PostAsJsonAsync(path, new CommandResultRequest(true, "Completed before network loss"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsJsonAsync(path, new CommandResultRequest(false, "Conflicting retry"))).StatusCode);
        Assert.True((await db.CommandResults.SingleAsync(r => r.CommandId == command.Id)).Succeeded);
    }

    private static string Cookie(HttpResponseMessage response, string name)
    {
        response.EnsureSuccessStatusCode();
        var prefix = name + "=";
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(prefix, StringComparison.Ordinal));
        return cookie[prefix.Length..cookie.IndexOf(';')];
    }
}
