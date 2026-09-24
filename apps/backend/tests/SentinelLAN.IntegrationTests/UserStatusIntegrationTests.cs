using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SentinelLAN.Application;

namespace SentinelLAN.IntegrationTests;

public sealed class UserStatusIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task LockRevokesActiveAccessAndRefreshAndUnlockRequiresNewLogin()
    {
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).StatusCode);

        var email = $"lock-{Guid.NewGuid():N}@sentinellan.local";
        const string password = "UniqueEmployeePassword123!";
        var create = await MutateAsync(admin, HttpMethod.Post, "/api/v1/users",
            new CreateUserRequest(email, "Lockable Employee", Roles.Employee, password, "Provision employee", true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreateUserResponse>();
        Assert.NotNull(created);

        using var employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", email, password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/v1/auth/session")).StatusCode);

        var path = $"/api/v1/users/{created.User.Id}/status";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await MutateAsync(admin, HttpMethod.Put, path, new SetUserStatusRequest("Locked", "Incident response", false))).StatusCode);
        var locked = await MutateAsync(admin, HttpMethod.Put, path,
            new SetUserStatusRequest("Locked", "Suspected credential exposure", true));
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/v1/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await employee.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", email, password))).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await MutateAsync(admin, HttpMethod.Put, path,
                new SetUserStatusRequest("Active", "Credentials rotated and verified", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/v1/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await employee.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", email, password))).StatusCode);

        var audit = await admin.GetFromJsonAsync<AuditLogDto[]>("/api/v1/audit-logs");
        Assert.NotNull(audit);
        Assert.Contains(audit, item => item.Action == "UserLocked" && item.Reason.Contains(created.User.Id.ToString(), StringComparison.Ordinal));
        Assert.Contains(audit, item => item.Action == "UserUnlocked");
    }

    private static async Task<HttpResponseMessage> MutateAsync<T>(HttpClient client, HttpMethod method, string path, T body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-SentinelLAN-CSRF", "1");
        return await client.SendAsync(request);
    }
}
