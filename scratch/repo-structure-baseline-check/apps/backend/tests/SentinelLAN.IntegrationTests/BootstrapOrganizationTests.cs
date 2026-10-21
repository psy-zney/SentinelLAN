using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class BootstrapOrganizationTests
{
    [Fact]
    public async Task EmptyDatabaseRequiresRealConfigurationAndCreatesOnlyInitialAdministrator()
    {
        await using var db = CreateDatabase();
        var hasher = new Pbkdf2PasswordHasher();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BootstrapOrganizationInitializer.EnsureCreatedAsync(db, hasher, "ops", "Operations", "admin@example.test", "local-demo-only"));
        Assert.False(await db.Organizations.AnyAsync());

        await BootstrapOrganizationInitializer.EnsureCreatedAsync(db, hasher,
            "ops", "Operations", "admin@example.test", "UniqueBootstrapPassword123!");
        await BootstrapOrganizationInitializer.EnsureCreatedAsync(db, hasher, null, null, null, null);

        var organization = await db.Organizations.SingleAsync();
        Assert.Equal("ops", organization.Code);
        var admin = await db.Users.SingleAsync();
        Assert.Equal(organization.Id, admin.OrganizationId);
        Assert.Equal(Roles.Admin, admin.Role);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.Verify("UniqueBootstrapPassword123!", admin.PasswordHash));
        Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.False(await db.Devices.AnyAsync());
        Assert.False(await db.EnrollmentTokens.AnyAsync());
    }

    [Fact]
    public async Task ExistingDemoWithKnownPasswordBlocksProductionStartup()
    {
        await using var db = CreateDatabase();
        var hasher = new Pbkdf2PasswordHasher();
        await DemoSeeder.SeedAsync(db, hasher, _ => null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BootstrapOrganizationInitializer.EnsureCreatedAsync(db, hasher, null, null, null, null));
    }

    private static SentinelDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<SentinelDbContext>()
            .UseInMemoryDatabase($"bootstrap-{Guid.NewGuid():N}").Options);
}
