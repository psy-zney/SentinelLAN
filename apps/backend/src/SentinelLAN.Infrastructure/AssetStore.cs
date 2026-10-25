using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AssetStore(SentinelDbContext dbContext) : IAssetStore
{
    public async Task<T> ExecuteWriteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!dbContext.Database.IsRelational()) return await action();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
    public async Task<IReadOnlyDictionary<Guid, AssetDeviceReference>> GetDeviceReferencesAsync(Guid org, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var selected = ids.Distinct().ToArray();
        return await dbContext.Devices.AsNoTracking().Where(x => x.OrganizationId == org && selected.Contains(x.Id))
            .Select(x => new AssetDeviceReference(x.Id, x.Name, x.AssignedUserId, x.IsRevoked)).ToDictionaryAsync(x => x.Id, ct);
    }
    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(Guid org, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var selected = ids.Distinct().ToArray();
        return await dbContext.Users.AsNoTracking().Where(x => x.OrganizationId == org && selected.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName }).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
    }
    public async Task<IReadOnlyList<IncidentTicket>> GetIncidentPageAsync(Guid org, Guid? device, Guid? employee, PageWindow page, CancellationToken ct)
    {
        page.Validate();
        return await dbContext.Incidents.AsNoTracking().Where(x => x.OrganizationId == org && (device == null || x.DeviceId == device) &&
                (employee == null || dbContext.Devices.Any(d => d.OrganizationId == org && d.Id == x.DeviceId && d.AssignedUserId == employee && !d.IsRevoked)))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<WorkOrder>> GetWorkOrderPageAsync(Guid org, Guid? device, Guid? employee, PageWindow page, CancellationToken ct)
    {
        page.Validate();
        return await dbContext.WorkOrders.AsNoTracking().Where(x => x.OrganizationId == org && (device == null || x.DeviceId == device) &&
                (employee == null || dbContext.Devices.Any(d => d.OrganizationId == org && d.Id == x.DeviceId && d.AssignedUserId == employee && !d.IsRevoked)))
            .OrderBy(x => x.Status == "Completed" ? 1 : 0).ThenByDescending(x => x.Priority == "Critical" ? 4 : x.Priority == "High" ? 3 : x.Priority == "Medium" ? 2 : 1)
            .ThenBy(x => x.DueDate ?? DateTimeOffset.MaxValue).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<AssetLoan>> GetLoanPageAsync(Guid org, Guid? device, PageWindow page, CancellationToken ct)
    {
        page.Validate();
        return await dbContext.AssetLoans.AsNoTracking().Where(x => x.OrganizationId == org && (device == null || x.DeviceId == device))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(page.Offset).Take(page.Limit).ToListAsync(ct);
    }
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
            .Where(x => (x.CollectedAt ?? x.CreatedAt) >= DateTimeOffset.UtcNow.AddDays(-TechnicalDataRetentionService.RetentionDays))
            .OrderByDescending(x => x.CollectedAt ?? x.CreatedAt)
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
