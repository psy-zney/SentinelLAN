using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SentinelLAN.Application;

namespace SentinelLAN.IntegrationTests;

public sealed class DeviceAdministrationIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task AdminCanEnrollAssignAndRevokeDeviceWithScopedAccessAndAudit()
    {
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var technician = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var agent = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(admin, "admin@sentinellan.local", "local-demo-only")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(technician, "technician@sentinellan.local", "local-demo-only")).StatusCode);

        var tokenRequest = new EnrollmentTokenRequest(15, "Prepare authorized test device", true);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendMutationAsync(technician, HttpMethod.Post, "/api/v1/enrollment-tokens", tokenRequest)).StatusCode);

        var tokenResponse = await SendMutationAsync(admin, HttpMethod.Post, "/api/v1/enrollment-tokens", tokenRequest);
        Assert.Equal(HttpStatusCode.Created, tokenResponse.StatusCode);
        Assert.Equal("no-store", tokenResponse.Headers.CacheControl?.ToString());
        var token = await tokenResponse.Content.ReadFromJsonAsync<EnrollmentTokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));

        var enrollRequest = new EnrollRequest(token.Token, $"ADMIN-LIFECYCLE-{Guid.NewGuid():N}", "Windows 11", "test");
        var enrollResponse = await agent.PostAsJsonAsync("/api/v1/agent/enroll", enrollRequest);
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);
        var enrolled = await enrollResponse.Content.ReadFromJsonAsync<EnrollResponse>();
        Assert.NotNull(enrolled);
        Assert.NotEqual(Guid.Empty, enrolled.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(enrolled.DeviceSecret));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await agent.PostAsJsonAsync("/api/v1/agent/enroll", enrollRequest)).StatusCode);

        var email = $"device-admin-{Guid.NewGuid():N}@sentinellan.local";
        const string password = "LifecyclePassword123!";
        var userResponse = await SendMutationAsync(admin, HttpMethod.Post, "/api/v1/users",
            new CreateUserRequest(email, "Device lifecycle employee", Roles.Employee, password, "Assign test device", true));
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        var createdUser = await userResponse.Content.ReadFromJsonAsync<CreateUserResponse>();
        Assert.NotNull(createdUser);

        using var employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employee, email, password)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync("/api/v1/my-device")).StatusCode);

        var assignmentPath = $"/api/v1/devices/{enrolled.DeviceId}/assignment";
        var assignment = new DeviceAssignmentRequest(createdUser.User.Id, "Assign authorized employee", true);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendMutationAsync(technician, HttpMethod.Put, assignmentPath, assignment)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendMutationAsync(admin, HttpMethod.Put, assignmentPath, assignment with { Confirmed = false })).StatusCode);

        var assignmentResponse = await SendMutationAsync(admin, HttpMethod.Put, assignmentPath, assignment);
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        var assigned = await assignmentResponse.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.NotNull(assigned);
        Assert.Equal(createdUser.User.Id, assigned.AssignedUserId);

        var myDeviceResponse = await employee.GetAsync("/api/v1/my-device");
        Assert.Equal(HttpStatusCode.OK, myDeviceResponse.StatusCode);
        var myDevice = await myDeviceResponse.Content.ReadFromJsonAsync<MyDeviceDto>();
        Assert.Equal(enrolled.DeviceId, myDevice?.Device.Id);

        var revokePath = $"/api/v1/devices/{enrolled.DeviceId}/revoke";
        var revoke = new RevokeDeviceRequest("Retire authorized test device", true);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendMutationAsync(technician, HttpMethod.Post, revokePath, revoke)).StatusCode);
        var revokeResponse = await SendMutationAsync(admin, HttpMethod.Post, revokePath, revoke);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        var revoked = await revokeResponse.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.NotNull(revoked);
        Assert.True(revoked.IsRevoked);
        Assert.Null(revoked.AssignedUserId);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync("/api/v1/my-device")).StatusCode);

        using var poll = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent/commands/poll");
        poll.Headers.Add("X-SentinelLAN-Device-Id", enrolled.DeviceId.ToString());
        poll.Headers.Add("X-SentinelLAN-Device-Secret", enrolled.DeviceSecret);
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.SendAsync(poll)).StatusCode);

        var audit = await admin.GetFromJsonAsync<AuditLogDto[]>($"/api/v1/audit-logs?deviceId={enrolled.DeviceId}");
        Assert.NotNull(audit);
        Assert.Contains(audit, item => item.Action == "DeviceAssigned");
        Assert.Contains(audit, item => item.Action == "DeviceRevoked");
        Assert.DoesNotContain(audit, item => item.Reason.Contains(token.Token, StringComparison.Ordinal));
        Assert.DoesNotContain(audit, item => item.Reason.Contains(enrolled.DeviceSecret, StringComparison.Ordinal));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", email, password));

    private static async Task<HttpResponseMessage> SendMutationAsync<T>(HttpClient client, HttpMethod method, string path, T body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-SentinelLAN-CSRF", "1");
        return await client.SendAsync(request);
    }
}
