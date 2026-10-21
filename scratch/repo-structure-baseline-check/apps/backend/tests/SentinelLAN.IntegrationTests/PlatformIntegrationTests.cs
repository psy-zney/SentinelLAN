using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class PlatformIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private const string OwnerEmail = "owner@platform.test";
    private const string OwnerPassword = "Platform-integration-secret-2026!";

    [Fact]
    public async Task OwnerCreatesRealCompanyInvitationAndSuspensionRevokesAccess()
    {
        using var owner = await OwnerAsync();
        var code = $"company-{Guid.NewGuid():N}";
        var confirmation = Confirm();
        var create = new CreateCompanyRequest(code, code, "admin@company.test", "Company Admin", confirmation);
        var response = await owner.PostAsJsonAsync("/api/v1/platform/companies", create);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitation = (await response.Content.ReadFromJsonAsync<CompanyInvitation>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Code = code + "x", Name = code + "x" })).StatusCode);
        var token = Uri.UnescapeDataString(invitation.ActivationUrl.Split("#token=")[1]);
        using var company = factory.CreateClient();
        company.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        var password = "Company-account-test-2026!";
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(token, password))).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(token, password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(code, "admin@company.test", password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/platform/companies")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/v1/devices")).StatusCode);

        var list = (await owner.GetFromJsonAsync<CompanySummary[]>("/api/v1/platform/companies"))!;
        Assert.Contains(list, c => c.Id == invitation.OrganizationId && c.Users == 1 && c.Devices == 0);
        Assert.DoesNotContain(list, c => c.Code == PlatformIdentity.OrganizationCode);
        var enrollment = (await (await company.PostAsJsonAsync("/api/v1/enrollment-tokens", new { validForMinutes = 15, reason = "Enroll test machine", confirmed = true })).Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/platform/companies/{invitation.OrganizationId}/status", new SetCompanyStatusRequest(true, Confirm()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsJsonAsync("/api/v1/mobile/auth/login", new MobileLoginRequest(code, "admin@company.test", password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await company.PostAsJsonAsync("/api/v1/agent/enroll", new EnrollRequest(enrollment.Token, "TEST-PC", "Windows", "test"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/platform/companies/{invitation.OrganizationId}/status", new SetCompanyStatusRequest(false, Confirm()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(code, "admin@company.test", password))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.True(await db.Organizations.AnyAsync(o => o.Id == invitation.OrganizationId));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "CompanySuspended" && a.Reason.Contains(invitation.OrganizationId.ToString())));
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Reason.Contains(token)));
    }

    [Fact]
    public async Task OwnerLoginIsSeparateAndMutationsRejectMissingCsrfExpiredOrUnconfirmedRequests()
    {
        using var owner = await OwnerAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(PlatformIdentity.OrganizationCode, OwnerEmail, OwnerPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformLoginRequest("admin@sentinellan.local", "local-demo-only"))).StatusCode);
        var create = new CreateCompanyRequest("invalid-request", "Invalid request", "admin@company.test", "Admin", Confirm() with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Confirmation = Confirm() with { Confirmed = false } })).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-SentinelLAN-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Confirmation = Confirm() })).StatusCode);
    }

    [Fact]
    public async Task OwnerCanInspectCompanyDetailsAndSystemHealth()
    {
        using var owner = await OwnerAsync();
        var code = $"inspect-{Guid.NewGuid():N}";
        var create = new CreateCompanyRequest(code, $"Inspect {code}", "inspector-admin@company.test", "Inspect Admin", Confirm());
        var response = await owner.PostAsJsonAsync("/api/v1/platform/companies", create);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitation = (await response.Content.ReadFromJsonAsync<CompanyInvitation>())!;

        // Inspect company details
        var detailResponse = await owner.GetAsync($"/api/v1/platform/companies/{invitation.OrganizationId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<CompanyDetailView>())!;
        Assert.Equal(invitation.OrganizationId, detail.Id);
        Assert.Equal(code, detail.Code);
        Assert.Single(detail.Users);
        Assert.Equal("inspector-admin@company.test", detail.Users[0].Email);
        Assert.False(detail.IsSuspended);

        // System status
        var statusResponse = await owner.GetAsync("/api/v1/platform/system/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = (await statusResponse.Content.ReadFromJsonAsync<PlatformSystemStatus>())!;
        Assert.True(status.DatabaseConnected);
        Assert.True(status.TotalCompanies >= 1);
        Assert.True(status.MemoryUsageMb > 0);
    }


    private async Task<HttpClient> OwnerAsync()
    {
        using var scope = factory.Services.CreateScope();
        await PlatformOwnerInitializer.EnsureCreatedAsync(scope.ServiceProvider.GetRequiredService<SentinelDbContext>(), scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), OwnerEmail, OwnerPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        var login = await client.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformLoginRequest(OwnerEmail, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("sentinellan.platform.access=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        return client;
    }
    private static PlatformConfirmation Confirm() => new("Provision authorized company", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(4));
}
