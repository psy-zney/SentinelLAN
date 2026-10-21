using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

// Device data is provided through a separate Application directory port.
public sealed class PreventiveDeviceReader(SentinelDbContext db) : IPreventiveDeviceReader
{
    public async Task<IReadOnlyList<Device>> GetDevicesAsync(Guid org, CancellationToken ct) =>
        await db.Devices.AsNoTracking().Where(value => value.OrganizationId == org && !value.IsRevoked && value.AssetStatus != "Retired").ToListAsync(ct);
}

public sealed class PreventiveMaintenanceStore(SentinelDbContext db) : IPreventiveMaintenanceStore
{
    public Task<bool> HasRunAsync(Guid org, DateOnly day, CancellationToken ct) =>
        db.Set<PreventiveMaintenanceRun>().AnyAsync(value => value.OrganizationId == org && value.Day == day, ct);
    public async Task<IReadOnlyList<WorkOrder>> GetPreventiveOrdersAsync(Guid org, CancellationToken ct) =>
        await db.WorkOrders.AsNoTracking().Where(value => value.OrganizationId == org && value.Type == "Preventive").ToListAsync(ct);
    public void Add(WorkOrder order) => db.Add(order);
    public void AddRun(PreventiveMaintenanceRun run) => db.Add(run);
    public void Audit(AuditLog audit) => db.Add(audit);
    public async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { db.ChangeTracker.Clear(); return false; }
    }
}
