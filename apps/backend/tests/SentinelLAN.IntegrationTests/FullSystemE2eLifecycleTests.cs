using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class FullSystemE2eLifecycleTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task CompleteSystemE2eWorkflowExecutesSuccessfully()
    {
        // 1. Health Checks
        using var client = factory.CreateClient();
        var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        var readyResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        // 2. Admin Authentication & Session
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))
        };
        loginRequest.Headers.Add("X-SentinelLAN-CSRF", "1");
        var loginResponse = await adminClient.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var session = await adminClient.GetFromJsonAsync<CurrentSessionResponse>("/api/v1/auth/session");
        Assert.NotNull(session);
        Assert.Equal(Roles.Admin, session.Role);

        // 3. Dashboard Metrics
        var dashboard = await adminClient.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(dashboard);
        Assert.True(dashboard.TotalDevices >= 0);

        // 4. Device Enrollment via Agent API (using dedicated Agent client without browser cookies)
        using var agentClient = factory.CreateClient();
        var enrollToken = $"e2e-enroll-{Guid.NewGuid():N}";
        await SeedEnrollmentTokenAsync(enrollToken, DateTimeOffset.UtcNow.AddHours(2));

        var agentEnrollRequest = new EnrollRequest(enrollToken, "E2E-TEST-NODE", "Windows 11 Pro", "1.0.0");
        var enrollResponse = await agentClient.PostAsJsonAsync("/api/v1/agent/enroll", agentEnrollRequest);
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);
        var enrollment = await enrollResponse.Content.ReadFromJsonAsync<EnrollResponse>();
        Assert.NotNull(enrollment);
        Assert.NotEqual(Guid.Empty, enrollment.DeviceId);

        // 5. Agent Telemetry & Heartbeat
        var heartbeat = new HeartbeatRequest(Guid.NewGuid().ToString("N"), 15.5, 42.0, 58.2, "Windows 11 Pro", "1.0.0");
        using var heartbeatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent/heartbeat")
        {
            Content = JsonContent.Create(heartbeat)
        };
        heartbeatRequest.Headers.Add("X-SentinelLAN-Device-Id", enrollment.DeviceId.ToString());
        heartbeatRequest.Headers.Add("X-SentinelLAN-Device-Secret", enrollment.DeviceSecret);
        var heartbeatResponse = await agentClient.SendAsync(heartbeatRequest);
        Assert.Equal(HttpStatusCode.Accepted, heartbeatResponse.StatusCode);

        // Verify Device Online in Admin API
        var deviceDetail = await adminClient.GetFromJsonAsync<DeviceDto>($"/api/v1/devices/{enrollment.DeviceId}");
        Assert.NotNull(deviceDetail);
        Assert.True(deviceDetail.IsOnline);

        // 6. Policy Management: Create Policy & Assign to Device
        using var createPolicyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/policies")
        {
            Content = JsonContent.Create(new CreatePolicyRequest("E2E Security Policy", 15, "ReadOnly"))
        };
        createPolicyReq.Headers.Add("X-SentinelLAN-CSRF", "1");
        var createPolicyResp = await adminClient.SendAsync(createPolicyReq);
        Assert.Equal(HttpStatusCode.Created, createPolicyResp.StatusCode);
        var createdPolicy = await createPolicyResp.Content.ReadFromJsonAsync<PolicyDto>();
        Assert.NotNull(createdPolicy);

        using var assignPolicyReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/policies/{createdPolicy.Id}/assign")
        {
            Content = JsonContent.Create(new AssignPolicyRequest(createdPolicy.Id, enrollment.DeviceId))
        };
        assignPolicyReq.Headers.Add("X-SentinelLAN-CSRF", "1");
        var assignPolicyResp = await adminClient.SendAsync(assignPolicyReq);
        Assert.Equal(HttpStatusCode.OK, assignPolicyResp.StatusCode);

        // 7. Safe Command Dispatch (with explicit confirmation as required by security policy)
        using var cmdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands")
        {
            Content = JsonContent.Create(new CreateCommandRequest(enrollment.DeviceId, "ShowNotification", "E2E verification notification test", 120, true, null))
        };
        cmdReq.Headers.Add("X-SentinelLAN-CSRF", "1");
        var cmdResp = await adminClient.SendAsync(cmdReq);
        Assert.Equal(HttpStatusCode.Created, cmdResp.StatusCode);
        var createdCmd = await cmdResp.Content.ReadFromJsonAsync<DeviceCommand>();
        Assert.NotNull(createdCmd);
        Assert.Equal(DeviceCommandStatus.Pending, createdCmd.Status);

        // 8. Agent Polls Command & Sends Execution Result
        using var pollReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent/commands/poll");
        pollReq.Headers.Add("X-SentinelLAN-Device-Id", enrollment.DeviceId.ToString());
        pollReq.Headers.Add("X-SentinelLAN-Device-Secret", enrollment.DeviceSecret);
        var pollResp = await agentClient.SendAsync(pollReq);
        Assert.Equal(HttpStatusCode.OK, pollResp.StatusCode);
        var polledCmd = await pollResp.Content.ReadFromJsonAsync<DeviceCommand>();
        Assert.NotNull(polledCmd);
        Assert.Equal(createdCmd.Id, polledCmd.Id);

        using var resultReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agent/commands/{polledCmd.Id}/result")
        {
            Content = JsonContent.Create(new CommandResultRequest(true, "Notification displayed successfully in E2E test"))
        };
        resultReq.Headers.Add("X-SentinelLAN-Device-Id", enrollment.DeviceId.ToString());
        resultReq.Headers.Add("X-SentinelLAN-Device-Secret", enrollment.DeviceSecret);
        var resultResp = await agentClient.SendAsync(resultReq);
        Assert.Equal(HttpStatusCode.Accepted, resultResp.StatusCode);

        // 9. Verify Immutable Audit Logs
        var auditLogs = await adminClient.GetFromJsonAsync<AuditLogDto[]>("/api/v1/audit-logs");
        Assert.NotNull(auditLogs);
        Assert.Contains(auditLogs, a => a.DeviceId == enrollment.DeviceId);

        // 10. Incident Alert Workflow: Create, Acknowledge & Resolve
        using var createAlertReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
        {
            Content = JsonContent.Create(new CreateAlertRequest(enrollment.DeviceId, "Warning", "E2E test simulated alert"))
        };
        createAlertReq.Headers.Add("X-SentinelLAN-CSRF", "1");
        var createAlertResp = await adminClient.SendAsync(createAlertReq);
        Assert.Equal(HttpStatusCode.Created, createAlertResp.StatusCode);
        var createdAlert = await createAlertResp.Content.ReadFromJsonAsync<AlertDto>();
        Assert.NotNull(createdAlert);

        using var ackReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/alerts/{createdAlert.Id}/acknowledge");
        ackReq.Headers.Add("X-SentinelLAN-CSRF", "1");
        var ackResp = await adminClient.SendAsync(ackReq);
        Assert.Equal(HttpStatusCode.OK, ackResp.StatusCode);

        using var resolveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/alerts/{createdAlert.Id}/resolve");
        resolveReq.Headers.Add("X-SentinelLAN-CSRF", "1");
        var resolveResp = await adminClient.SendAsync(resolveReq);
        Assert.Equal(HttpStatusCode.OK, resolveResp.StatusCode);
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
}
