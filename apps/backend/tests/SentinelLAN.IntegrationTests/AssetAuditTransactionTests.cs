using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AssetAuditTransactionTests
{
    [PostgreSqlFact]
    public async Task AuditInsertFailureRollsBackAnAlreadySavedAssetChange()
    {
        await using var factory = new SentinelApiFactory();
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SentinelDbContext>>();
        await using var db = new SentinelDbContext(new DbContextOptionsBuilder<SentinelDbContext>(options).AddInterceptors(new RejectAudit()).Options);
        var device = await db.Devices.FirstAsync();
        var originalName = device.Name;
        var id = device.Id;
        var store = new AssetStore(db);
        await Assert.ThrowsAsync<IOException>(() => store.ExecuteWriteAsync(async () =>
        {
            device.Name = "changed-before-failure";
            await store.UpdateDeviceAsync(device);
            await store.RecordAuditAsync(new AuditLog
            {
                OrganizationId = device.OrganizationId,
                ActorId = Guid.NewGuid(),
                DeviceId = device.Id,
                Action = "Fixture",
                Reason = "dummy failure",
                Outcome = "Success"
            });
            return true;
        }, default));
        Assert.Equal(originalName, await db.Devices.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).SingleAsync());
        Assert.False(await db.AuditLogs.AnyAsync(x => x.DeviceId == id && x.Action == "Fixture"));
    }

    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added)) throw new IOException("Dummy audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
