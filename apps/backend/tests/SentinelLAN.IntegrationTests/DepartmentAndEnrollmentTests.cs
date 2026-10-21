using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Api;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class DepartmentAndEnrollmentTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private static AdministrativeConfirmation Confirmation() => new("Authorized fixture change", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
    [Fact]
    public async Task DepartmentPolicyAndBulkActionsStayInTenantAndReplaysDoNotQueueCommandsTwice()
    {
        using var admin = factory.CreateClient(); admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var actorUser = await db.Users.SingleAsync(x => x.Email == "admin@sentinellan.local");
        var policy = await db.Policies.FirstAsync(x => x.OrganizationId == actorUser.OrganizationId);
        var device = new Device { OrganizationId = actorUser.OrganizationId, Name = "Department fixture", OsVersion = "test", AgentVersion = "test" };
        db.Add(device); await db.SaveChangesAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/departments", new SaveDepartmentRequest("Accounting fixture", policy.Id, Guid.Empty, Confirmation()));
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        var directory = (await admin.GetFromJsonAsync<DepartmentDirectoryDto>("/api/v1/departments"))!;
        var department = Assert.Single(directory.Departments);
        Assert.Equal(policy.Id, department.PolicyId);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/departments/devices/{device.Id}", new DepartmentMembershipRequest(department.Id, department.Revision, Confirmation()))).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal(department.Id, (await db.Devices.AsNoTracking().SingleAsync(x => x.Id == device.Id)).DepartmentId);
        Assert.Equal(policy.Id, await scope.ServiceProvider.GetRequiredService<IDepartmentPolicyReader>().GetPolicyIdAsync(actorUser.OrganizationId, device.Id, default));
        var assigned = await scope.ServiceProvider.GetRequiredService<PolicyService>().GetAssignedPolicyAsync(new(DeviceId: device.Id, OrganizationId: actorUser.OrganizationId), default);
        Assert.Equal(policy.Id, assigned!.Id);
        var batch = new BulkCommandRequest([device.Id, Guid.NewGuid()], "CollectTelemetryNow", Confirmation());
        var response = await admin.PostAsJsonAsync("/api/v1/commands/bulk", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.Content.ReadFromJsonAsync<BulkCommandResult[]>())!;
        Assert.Equal("Queued", results[0].Outcome); Assert.Equal("Unavailable", results[1].Outcome);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/commands/bulk", batch)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/commands/bulk", batch with { Type = "IsolateNetwork", Confirmation = Confirmation() })).StatusCode);
        Assert.Single(await db.Commands.Where(x => x.DeviceId == device.Id && x.Type == "CollectTelemetryNow").ToListAsync());
        Assert.Single(await db.Commands.Where(x => x.DeviceId == device.Id && x.Type == "RefreshPolicy").ToListAsync());
        Assert.True(await db.Set<OperationalOutboxMessage>().AnyAsync(x => x.DeviceId == device.Id && x.Kind == "CommandQueued"));
        using var employee = factory.CreateClient(); employee.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await employee.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/departments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/commands/bulk", batch)).StatusCode);
    }
    [Fact]
    public async Task EnrollmentBatchProducesSeparateFifteenMinuteTokensAndReservesOneNonce()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        // Ensure the application's fixture has initialized its seed.
        using var client = factory.CreateClient(); await client.GetAsync("/health");
        var user = await db.Users.SingleAsync(x => x.Email == "admin@sentinellan.local"); var actor = new ActorContext(UserId: user.Id, OrganizationId: user.OrganizationId, Roles.Admin);
        var service = scope.ServiceProvider.GetRequiredService<EnrollmentBatchService>(); var input = new EnrollmentBatchRequest(2, "Windows", Confirmation());
        var tokens = await service.CreateAsync(actor, input, default);
        Assert.Equal(2, tokens.Count); Assert.NotEqual(tokens[0].Token, tokens[1].Token);
        Assert.All(tokens, x => Assert.InRange(x.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16)));
        Assert.Equal(409, (await Assert.ThrowsAsync<OrganizationConfigurationException>(() => service.CreateAsync(actor, input, default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<OrganizationConfigurationException>(() => service.CreateAsync(actor, input with { Count = 51, Confirmation = Confirmation() }, default))).StatusCode);
    }
    [Fact]
    public void BootstrapContainsOnlyOneTokenAndVerifiesReleaseBeforeRunningIt()
    {
        var settings = new EnrollmentBootstrapSettings("https://sentinel.example.test", "PUBLIC", "key-1", "https://releases.example.test/agent.msi", new string('A', 64), new string('B', 40));
        var package = EnrollmentBootstrap.Create(settings, new string('C', 64), DateTimeOffset.UtcNow.AddMinutes(15), "Windows", 1);
        Assert.Contains("SL1.", package.ConnectionCode); Assert.Contains("Get-AuthenticodeSignature", package.Script); Assert.Contains("Get-FileHash", package.Script);
        Assert.True(package.Script.IndexOf("Get-FileHash", StringComparison.Ordinal) < package.Script.IndexOf("$installed = Start-Process", StringComparison.Ordinal));
        Assert.Contains("AllowAutoRedirect = $false", package.Script);
        Assert.DoesNotContain("ExecutionPolicy Bypass", package.Script);
    }
}
