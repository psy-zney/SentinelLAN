using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AssetStore(SentinelDbContext dbContext) : IAssetStore
{
    public async Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken = default) =>
        await dbContext.Devices.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == deviceId, cancellationToken);

    public async Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Users.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == userId, cancellationToken);

    public async Task<string?> GetUserNameAsync(Guid organizationId, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue) return null;
        return await dbContext.Users
            .Where(x => x.OrganizationId == organizationId && x.Id == userId.Value)
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetrySnapshot>> GetRecentTelemetryAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken = default) =>
        await dbContext.Telemetry
            .Where(x => x.OrganizationId == organizationId && x.DeviceId == deviceId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceCommand>> GetDeviceCommandsAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken = default) =>
        await dbContext.Commands
            .Where(x => x.OrganizationId == organizationId && x.DeviceId == deviceId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task UpdateDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        device.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.Devices.Update(device);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Incidents.Where(x => x.OrganizationId == organizationId);
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IncidentTicket?> FindIncidentAsync(Guid organizationId, Guid incidentId, CancellationToken cancellationToken = default) =>
        await dbContext.Incidents.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == incidentId, cancellationToken);

    public Task<IncidentTicket?> FindIncidentByIdempotencyKeyAsync(Guid organizationId, Guid reportedByUserId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        dbContext.Incidents.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.ReportedByUserId == reportedByUserId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IncidentCreateResult> CreateIncidentAsync(IncidentTicket incident, AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        dbContext.Incidents.Add(incident);
        dbContext.AuditLogs.Add(auditLog);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IncidentCreateResult(incident, false, false);
        }
        catch (DbUpdateException exception) when (incident.IdempotencyKey is not null &&
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(incident).State = EntityState.Detached;
            dbContext.Entry(auditLog).State = EntityState.Detached;
            var existing = await FindIncidentByIdempotencyKeyAsync(incident.OrganizationId, incident.ReportedByUserId, incident.IdempotencyKey, cancellationToken);
            if (existing is null) throw;
            return new IncidentCreateResult(existing, true, existing.RequestFingerprint != incident.RequestFingerprint);
        }
    }

    public async Task UpdateIncidentAsync(IncidentTicket incident, CancellationToken cancellationToken = default)
    {
        incident.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.Incidents.Update(incident);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.WorkOrders.Where(x => x.OrganizationId == organizationId);
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<WorkOrder?> FindWorkOrderAsync(Guid organizationId, Guid workOrderId, CancellationToken cancellationToken = default) =>
        await dbContext.WorkOrders.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == workOrderId, cancellationToken);

    public async Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return workOrder;
    }

    public async Task UpdateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        workOrder.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.WorkOrders.Update(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssetLoan>> GetAssetLoansAsync(Guid organizationId, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AssetLoans.Where(x => x.OrganizationId == organizationId);
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<AssetLoan?> FindAssetLoanAsync(Guid organizationId, Guid loanId, CancellationToken cancellationToken = default) =>
        await dbContext.AssetLoans.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == loanId, cancellationToken);

    public async Task<AssetLoan> CreateAssetLoanAsync(AssetLoan loan, CancellationToken cancellationToken = default)
    {
        dbContext.AssetLoans.Add(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return loan;
    }

    public async Task UpdateAssetLoanAsync(AssetLoan loan, CancellationToken cancellationToken = default)
    {
        loan.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AssetLoans.Update(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
