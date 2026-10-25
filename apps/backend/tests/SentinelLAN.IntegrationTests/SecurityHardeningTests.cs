using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Api;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class SecurityHardeningTests
{
    [PostgreSqlFact]
    public async Task ConcurrentDatabaseScopesShareOneAccountBudget()
    {
        await using var factory = new SentinelApiFactory();
        var services = factory.Services;
        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ILoginAttemptLimiter>()
                .ReserveAsync("dummy-company", "concurrent@example.test", default);
        }));
        Assert.Equal(5, attempts.Count(x => x is null));
        Assert.Single(attempts, x => x is not null);
    }

    [Fact]
    public async Task IngressRejectsExcessRequestsBeforeDownstreamAuthenticationWork()
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        using var budget = new IngressRequestBudget(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SENTINELLAN_INGRESS_BURST"] = "1" }).Build());
        var authenticationCalls = 0;
        var middleware = new IngressRateLimitMiddleware(_ => { authenticationCalls++; return Task.CompletedTask; }, budget);
        DefaultHttpContext Context() { var context = new DefaultHttpContext { RequestServices = services }; context.Request.Path = "/api/v1/auth/session"; return context; }
        await middleware.InvokeAsync(Context());
        var rejected = Context();
        await middleware.InvokeAsync(rejected);
        Assert.Equal(1, authenticationCalls);
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.True(rejected.Response.Headers.ContainsKey("Retry-After"));
    }

    [Theory]
    [InlineData("/api/v1/users?limit=201")]
    [InlineData("/api/v1/devices?offset=-1")]
    [InlineData("/api/v1/self-service/requests?limit=0")]
    [InlineData("/api/v1/incidents?offset=1000001")]
    public async Task InvalidPaginationIsRejected(string path)
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public void RejectedAttemptsDoNotExtendTheAccountDelayAndExpiryResetsIt()
    {
        var now = DateTimeOffset.UtcNow;
        var bucket = new LoginAttemptBucket { PartitionHash = "dummy" };
        for (var i = 0; i < 5; i++) Assert.Null(bucket.Reserve(now));
        var expiry = bucket.ExpiresAt;
        Assert.Equal(TimeSpan.FromSeconds(1), bucket.Reserve(now));
        Assert.Equal(expiry, bucket.ExpiresAt);
        Assert.Null(bucket.Reserve(now.AddSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(2), bucket.Reserve(now.AddSeconds(1)));
        Assert.Null(bucket.Reserve(expiry.AddMinutes(1)));
        Assert.Equal(1, bucket.Attempts);
    }

    [Fact]
    public async Task WebAndMobileShareTheNormalizedAccountBudget()
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest(" DEMO ", " Missing@Example.Test ", "incorrect"))).StatusCode);
        var rejected = await client.PostAsJsonAsync("/api/v1/mobile/auth/login",
            new MobileLoginRequest("demo", "missing@example.test", "incorrect"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("other-tenant", "missing@example.test", "incorrect"))).StatusCode);
    }

    [Fact]
    public async Task LogoutAllImmediatelyRejectsOtherAccessAndRefreshTokens()
    {
        await using var factory = new SentinelApiFactory();
        using var client = factory.CreateClient();
        var first = await (await client.PostAsJsonAsync("/api/v1/mobile/auth/login",
            new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).Content.ReadFromJsonAsync<MobileAuthSessionResponse>();
        var second = await (await client.PostAsJsonAsync("/api/v1/mobile/auth/login",
            new MobileLoginRequest("demo", "admin@sentinellan.local", "local-demo-only"))).Content.ReadFromJsonAsync<MobileAuthSessionResponse>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/mobile/auth/logout-all", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/mobile/auth/refresh",
            new MobileRefreshRequest(second.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task PasswordHashCannotBeSubmittedAsThePassword()
    {
        await using var factory = new SentinelApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var hash = await db.Users.Where(x => x.Email == "admin@sentinellan.local").Select(x => x.PasswordHash).SingleAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", "admin@sentinellan.local", hash))).StatusCode);
        var hasher = new Pbkdf2PasswordHasher();
        var legacy = SecretHash.Create("dummy-legacy-password");
        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(legacy, legacy));
        Assert.Equal(PasswordVerificationResult.Failed, new Pbkdf2PasswordHasher(allowLegacySha256: false).Verify("dummy-legacy-password", legacy));
        Assert.Equal(PasswordVerificationResult.Failed, new Pbkdf2PasswordHasher(legacyUntil: DateTimeOffset.UtcNow.AddSeconds(-1), allowLegacySha256: false).Verify("dummy-legacy-password", legacy));
        Assert.Equal(PasswordVerificationResult.SuccessNeedsRehash, new Pbkdf2PasswordHasher(legacyUntil: DateTimeOffset.UtcNow.AddMinutes(1), allowLegacySha256: false).Verify("dummy-legacy-password", legacy));
    }
}
