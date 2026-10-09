using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class TestDatabaseIsolationTests
{
    [Fact]
    public async Task LockingAnAccountInOneFactoryDoesNotChangeAnotherFactorysSeededAccount()
    {
        using var first = new SentinelApiFactory();
        await using var second = new SentinelApiFactory();
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        using (var scope = first.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var employee = await db.Users.SingleAsync(user => user.Email == "employee@sentinellan.local");
            employee.Status = UserStatuses.Locked;
            await db.SaveChangesAsync();
        }

        var login = new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only");
        Assert.Equal(HttpStatusCode.Unauthorized, (await firstClient.PostAsJsonAsync("/api/v1/auth/login", login)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await secondClient.PostAsJsonAsync("/api/v1/auth/login", login)).StatusCode);
    }
}
