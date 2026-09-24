using Microsoft.EntityFrameworkCore;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class MyDeviceStore(SentinelDbContext db) : IMyDeviceStore
{
    public Task<Device?> FindAssignedDeviceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Devices.FirstOrDefaultAsync(
            d => d.OrganizationId == organizationId && d.AssignedUserId == userId && !d.IsRevoked, cancellationToken);

    public Task<TelemetrySnapshot?> GetLatestTelemetryAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) =>
        db.Telemetry
            .Where(t => t.OrganizationId == organizationId && t.DeviceId == deviceId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> GetAppliedPolicyNameAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken)
    {
        var assignment = await db.PolicyAssignments
            .Where(pa => pa.OrganizationId == organizationId && pa.DeviceId == deviceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null) return null;

        var policy = await db.Policies
            .Where(p => p.OrganizationId == organizationId && p.Id == assignment.PolicyId)
            .FirstOrDefaultAsync(cancellationToken);

        return policy?.Name;
    }

    public async Task<IReadOnlyList<IncidentTicket>> GetUserDeviceIncidentsAsync(Guid organizationId, Guid deviceId, Guid userId, CancellationToken cancellationToken) =>
        await db.Incidents
            .Where(i => i.OrganizationId == organizationId && i.DeviceId == deviceId && i.ReportedByUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TelemetrySnapshot>> GetDeviceTelemetryHistoryAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken) =>
        await db.Telemetry
            .Where(t => t.OrganizationId == organizationId && t.DeviceId == deviceId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> GetDeviceAuditActionsAsync(Guid organizationId, Guid deviceId, int limit, CancellationToken cancellationToken) =>
        await db.AuditLogs
            .Where(a => a.OrganizationId == organizationId && a.DeviceId == deviceId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<IncidentTicket?> FindIncidentByIdempotencyKeyAsync(Guid organizationId, Guid reportedByUserId, string idempotencyKey, CancellationToken cancellationToken) =>
        db.Incidents.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.ReportedByUserId == reportedByUserId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IncidentCreateResult> CreateIncidentAsync(IncidentTicket incident, AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.Incidents.Add(incident);
        db.AuditLogs.Add(auditLog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new IncidentCreateResult(incident, false, false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(incident).State = EntityState.Detached;
            db.Entry(auditLog).State = EntityState.Detached;
            var existing = await FindIncidentByIdempotencyKeyAsync(incident.OrganizationId, incident.ReportedByUserId, incident.IdempotencyKey!, cancellationToken);
            if (existing is null) throw;
            return new IncidentCreateResult(existing, true, existing.RequestFingerprint != incident.RequestFingerprint);
        }
    }
}
