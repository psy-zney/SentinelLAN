using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AttachmentQuotaTests
{
    [Fact]
    public async Task ConcurrentUploadsAcrossRequestsShareAnAtomicUserQuota()
    {
        await using var factory = new SentinelApiFactory();
        var errors = new CapturedErrors();
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(errors)).ConfigureServices(services =>
        {
            services.RemoveAll<AttachmentQuotaSettings>();
            services.AddSingleton(new AttachmentQuotaSettings(4096, 8192, 16384));
        }));
        Guid first;
        Guid second;
        using (var scope = configured.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == "employee@sentinellan.local");
            var device = await db.Devices.SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.AssignedUserId == user.Id && !x.IsRevoked)
                ?? new Device { OrganizationId = user.OrganizationId, AssignedUserId = user.Id, Name = "Quota fixture", OsVersion = "dummy", AgentVersion = "dummy" };
            if (db.Entry(device).State == EntityState.Detached) db.Add(device);
            var requests = Enumerable.Range(0, 2).Select(_ => new SelfServiceRequest
            {
                OrganizationId = user.OrganizationId,
                DeviceId = device.Id,
                UserId = user.Id,
                DeviceName = device.Name,
                UserName = user.DisplayName,
                Kind = "Incident",
                Category = "Other",
                Title = "Quota fixture",
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                RequestFingerprint = "dummy"
            }).ToArray();
            db.AddRange(requests);
            await db.SaveChangesAsync();
            var dayStart = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
            Assert.Equal(new AttachmentUsage(0, 0, 0), await new SelfServiceStore(db).GetAttachmentUsageAsync(user.OrganizationId, user.Id, dayStart, default));
            first = requests[0].Id;
            second = requests[1].Id;
        }
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only"))).StatusCode);
        client.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        var image = new CreateSupportAttachment("dummy.png", "image/png", Convert.ToBase64String(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/v1/self-service/requests/{first}/attachments", image),
            client.PostAsJsonAsync($"/api/v1/self-service/requests/{second}/attachments", image));
        Assert.True(responses.Any(x => x.IsSuccessStatusCode), string.Join("\n", errors.Exceptions));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.TooManyRequests);
        using var check = configured.Services.CreateScope();
        Assert.Equal(8, await check.ServiceProvider.GetRequiredService<SentinelDbContext>().SelfServiceAttachments.SumAsync(x => x.Size));
    }

    private sealed class CapturedErrors : ILoggerProvider, ILogger
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Exceptions { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Exceptions.Enqueue(exception.ToString());
        }
        public void Dispose() { }
    }
}
