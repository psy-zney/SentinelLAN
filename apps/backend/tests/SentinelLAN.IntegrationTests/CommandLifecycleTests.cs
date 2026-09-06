using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class CommandLifecycleTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Theory]
    [InlineData("ShowNotification")]
    [InlineData("CollectTelemetryNow")]
    [InlineData("RefreshPolicy")]
    [InlineData("SimulateLock")]
    [InlineData("SimulateNetworkIsolation")]
    public async Task SafeCommandCompletesOnceAndPreservesCreationAudit(string type)
    {
        var device = await SeedDeviceAsync();
        using var admin = await OperatorAsync();
        using var agent = AgentClient(device.Id);
        var command = await CreateAsync(admin, device.Id, type);
        var delivered = await agent.PostAsync("/api/v1/agent/commands/poll", null);
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal(command.Id, (await delivered.Content.ReadFromJsonAsync<DeviceCommand>())!.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await agent.PostAsync("/api/v1/agent/commands/poll", null)).StatusCode);
        var path = $"/api/v1/agent/commands/{command.Id}/result";
        Assert.Equal(HttpStatusCode.Accepted, (await agent.PostAsJsonAsync(path, new CommandResultRequest(true, "Simulated only"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsJsonAsync(path, new CommandResultRequest(false, "Conflicting retry"))).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.True((await db.CommandResults.SingleAsync(result => result.CommandId == command.Id)).Succeeded);
        var audits = await db.AuditLogs.Where(audit => audit.DeviceId == device.Id).ToListAsync();
        Assert.Contains(audits, audit => audit.Action == $"CommandCreated:{type}" && audit.Outcome == "Pending");
        Assert.Contains(audits, audit => audit.Action == $"CommandCompleted:{command.Id:N}:{type}" && audit.Outcome == "Succeeded");
    }

    [Fact]
    public async Task TwoSameTypeCommandsHaveIndependentResultsAndConcurrentPollDoesNotReplay()
    {
        var device = await SeedDeviceAsync();
        using var admin = await OperatorAsync();
        using var agent = AgentClient(device.Id);
        var first = await CreateAsync(admin, device.Id, "SimulateLock");
        var polls = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => agent.PostAsync("/api/v1/agent/commands/poll", null)));
        Assert.Single(polls, response => response.StatusCode == HttpStatusCode.OK);
        var second = await CreateAsync(admin, device.Id, "SimulateLock");
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsync("/api/v1/agent/commands/poll", null)).StatusCode);
        foreach (var command in new[] { first, second })
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => agent.PostAsJsonAsync($"/api/v1/agent/commands/{command.Id}/result", new CommandResultRequest(true, "Simulated"))));
            Assert.Single(results, response => response.StatusCode == HttpStatusCode.Accepted);
            Assert.Equal(2, results.Count(response => response.StatusCode == HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task RejectsMissingConfirmationUnsupportedTypeExpiryPendingResultAndForeignDevice()
    {
        var device = await SeedDeviceAsync();
        using var admin = await OperatorAsync();
        using var agent = AgentClient(device.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(device.Id, "SimulateLock", "Test"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(device.Id, "ExecutePowerShell", "Test", Confirmed: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(device.Id, "SimulateLock", " ", Confirmed: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(device.Id, "SimulateLock", "Test", 901, true))).StatusCode);
        var command = await CreateAsync(admin, device.Id, "SimulateLock");
        Assert.Equal(HttpStatusCode.Conflict, (await agent.PostAsJsonAsync($"/api/v1/agent/commands/{command.Id}/result", new CommandResultRequest(true, "Not delivered"))).StatusCode);
        var foreign = await SeedDeviceAsync(Guid.NewGuid());
        using var foreignAgent = AgentClient(foreign.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(foreign.Id, "SimulateLock", "Test", Confirmed: true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignAgent.PostAsJsonAsync($"/api/v1/agent/commands/{command.Id}/result", new CommandResultRequest(true, "Spoofed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await foreignAgent.PostAsync("/api/v1/agent/commands/poll", null)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var expired = new DeviceCommand { OrganizationId = device.OrganizationId, DeviceId = device.Id, IssuedByUserId = command.IssuedByUserId, Type = "SimulateLock", Reason = "Expired", Nonce = Guid.NewGuid().ToString("N"), Signature = "unused", IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), Status = DeviceCommandStatus.Delivered };
        db.Add(expired);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await agent.PostAsJsonAsync($"/api/v1/agent/commands/{expired.Id}/result", new CommandResultRequest(true, "Too late"))).StatusCode);
    }

    [Fact]
    public async Task TechnicianCanDispatchButEmployeeCannotAndAuditCannotBeRewritten()
    {
        var device = await SeedDeviceAsync();
        using var technician = await OperatorAsync("technician");
        using var employee = await OperatorAsync("employee");
        await CreateAsync(technician, device.Id, "SimulateLock");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(device.Id, "SimulateLock", "Test", Confirmed: true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync("/api/v1/audit-logs")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var audit = await db.AuditLogs.FirstAsync(item => item.DeviceId == device.Id);
        audit.Outcome = "Rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(audit).State = EntityState.Unchanged;
        db.Remove(audit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void AgentVerifiesServerSignatureAfterDatabaseTimestampRoundingAndRejectsTamperingReplayAndWrongDevice()
    {
        const string key = "integration-command-signing-key";
        var signer = new HmacCommandSigner(key);
        var now = DateTimeOffset.UtcNow;
        var command = new DeviceCommand { OrganizationId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IssuedByUserId = Guid.NewGuid(), Type = "SimulateLock", Reason = "Verified reason", Nonce = Guid.NewGuid().ToString("N"), Signature = "pending", IssuedAt = now, ExpiresAt = now.AddMinutes(1), Status = DeviceCommandStatus.Pending };
        command.Signature = signer.Sign(command);
        var remote = JsonSerializer.Deserialize<RemoteCommand>(JsonSerializer.Serialize(command))!;
        remote = remote with { IssuedAt = new DateTimeOffset(remote.IssuedAt.Ticks / 10 * 10, TimeSpan.Zero), ExpiresAt = new DateTimeOffset(remote.ExpiresAt.Ticks / 10 * 10, TimeSpan.Zero) };
        var signatures = new HmacCommandVerifier(key);
        Assert.True(signatures.Verify(remote));
        Assert.False(signatures.Verify(remote with { Reason = "Tampered" }));
        Assert.False(signatures.Verify(remote with { OrganizationId = Guid.NewGuid() }));
        Assert.False(signatures.Verify(remote with { Signature = "malformed" }));
        Assert.False(new HmacCommandVerifier(null).Verify(remote));
        var verifier = new CommandVerifier();
        Assert.False(verifier.TryAccept(remote, Guid.NewGuid(), now, signatures.Verify, out _));
        Assert.True(verifier.TryAccept(remote, remote.DeviceId, now, signatures.Verify, out _));
        Assert.False(verifier.TryAccept(remote, remote.DeviceId, now, signatures.Verify, out _));
        var duplicateNonce = JsonSerializer.Deserialize<DeviceCommand>(JsonSerializer.Serialize(remote with { Id = Guid.NewGuid() }))!;
        duplicateNonce.Signature = signer.Sign(duplicateNonce);
        var duplicateEnvelope = JsonSerializer.Deserialize<RemoteCommand>(JsonSerializer.Serialize(duplicateNonce))!;
        Assert.True(signatures.Verify(duplicateEnvelope));
        Assert.False(verifier.TryAccept(duplicateEnvelope, remote.DeviceId, now, signatures.Verify, out var replayReason));
        Assert.Equal("Replay detected", replayReason);
        Assert.False(new CommandVerifier().TryAccept(remote, remote.DeviceId, remote.ExpiresAt, signatures.Verify, out _));
    }

    private async Task<Device> SeedDeviceAsync(Guid? organizationId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var device = new Device { OrganizationId = organizationId ?? (await db.Organizations.SingleAsync(org => org.Code == "demo")).Id, Name = "Command test", OsVersion = "Windows", AgentVersion = "test" };
        db.Add(device);
        db.Add(new DeviceCredential { OrganizationId = device.OrganizationId, DeviceId = device.Id, SecretHash = SecretHash.Create("test-device-secret") });
        await db.SaveChangesAsync();
        return device;
    }

    private async Task<HttpClient> OperatorAsync(string role = "admin")
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", $"{role}@sentinellan.local", "local-demo-only"))).StatusCode);
        client.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        return client;
    }

    private HttpClient AgentClient(Guid deviceId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Id", deviceId.ToString());
        client.DefaultRequestHeaders.Add("X-SentinelLAN-Device-Secret", "test-device-secret");
        return client;
    }

    private static async Task<DeviceCommand> CreateAsync(HttpClient client, Guid deviceId, string type)
    {
        var response = await client.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(deviceId, type, "Authorized integration simulation", Confirmed: true));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeviceCommand>())!;
    }
}
