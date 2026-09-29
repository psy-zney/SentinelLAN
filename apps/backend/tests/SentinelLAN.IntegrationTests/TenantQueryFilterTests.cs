using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class TenantQueryFilterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueriesWithoutTenantScopeSupportTrustedInitialization(bool hasProvider)
    {
        var options = CreateOptions();
        await SeedAsync(options, Guid.NewGuid(), Guid.NewGuid());

        await using var db = new SentinelDbContext(options, hasProvider ? new MutableTenantProvider() : null);

        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(2, await db.Devices.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task CachedModelUsesTheTenantOfEachContext()
    {
        var options = CreateOptions();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await SeedAsync(options, first, second);

        await using var firstDb = new SentinelDbContext(options, new MutableTenantProvider { CurrentOrganizationId = first });
        await using var secondDb = new SentinelDbContext(options, new MutableTenantProvider { CurrentOrganizationId = second });
        await using var unscopedDb = new SentinelDbContext(options);

        Assert.Same(firstDb.Model, secondDb.Model);
        Assert.Same(firstDb.Model, unscopedDb.Model);
        Assert.Equal(first, (await firstDb.Users.SingleAsync()).OrganizationId);
        Assert.Equal(second, (await secondDb.Users.SingleAsync()).OrganizationId);
        Assert.Equal(first, (await firstDb.Devices.SingleAsync()).OrganizationId);
        Assert.Equal(second, (await secondDb.Devices.SingleAsync()).OrganizationId);
        Assert.Equal(first, (await firstDb.AuditLogs.SingleAsync()).OrganizationId);
        Assert.Equal(second, (await secondDb.AuditLogs.SingleAsync()).OrganizationId);
        Assert.Equal(2, await unscopedDb.Users.CountAsync());
        Assert.Equal(first, (await firstDb.Users.SingleAsync()).OrganizationId);
    }

    [Fact]
    public async Task QueriesUseTenantScopeEstablishedAfterContextCreation()
    {
        var options = CreateOptions();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await SeedAsync(options, first, second);
        var provider = new MutableTenantProvider();
        await using var db = new SentinelDbContext(options, provider);

        Assert.Equal(2, await db.Users.CountAsync());
        provider.CurrentOrganizationId = first;
        Assert.Equal(first, (await db.Users.SingleAsync()).OrganizationId);
        provider.CurrentOrganizationId = second;
        Assert.Equal(second, (await db.Users.SingleAsync()).OrganizationId);
        Assert.Equal(second, (await db.Devices.SingleAsync()).OrganizationId);
        Assert.Equal(second, (await db.AuditLogs.SingleAsync()).OrganizationId);
    }

    private static DbContextOptions<SentinelDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<SentinelDbContext>()
            .UseInMemoryDatabase($"tenant-filter-{Guid.NewGuid():N}").Options;

    private static async Task SeedAsync(DbContextOptions<SentinelDbContext> options, params Guid[] organizationIds)
    {
        await using var db = new SentinelDbContext(options);
        foreach (var id in organizationIds)
        {
            db.Add(new User
            {
                OrganizationId = id,
                Email = $"admin-{id:N}@tenant.test",
                DisplayName = "Test admin",
                Role = Roles.Admin,
                PasswordHash = "unused-test-hash"
            });
            db.Add(new Device { OrganizationId = id, Name = "Test device", OsVersion = "Windows", AgentVersion = "test" });
            db.Add(new AuditLog { OrganizationId = id, ActorId = Guid.NewGuid(), Action = "TestSeeded", Reason = "Tenant filter regression", Outcome = "Success" });
        }
        await db.SaveChangesAsync();
    }

    private sealed class MutableTenantProvider : ICurrentTenantProvider
    {
        public Guid? CurrentOrganizationId { get; set; }
    }
}
