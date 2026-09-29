using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class PlatformStore(SentinelDbContext db) : IPlatformStore
{
    // PlatformService checks PlatformOwner before calling this store. Cross-company queries
    // bypass the request tenant filter; company-specific queries retain explicit tenant predicates.
    public async Task<IReadOnlyList<CompanySummary>> ListAsync(CancellationToken ct)
    {
        var onlineSince = DateTimeOffset.UtcNow.AddMinutes(-2);
        return await db.Organizations.AsNoTracking().Where(o => o.Code != PlatformIdentity.OrganizationCode).OrderBy(o => o.Name)
            .Select(o => new CompanySummary(o.Id, o.Code, o.Name, o.IsSuspended,
                db.Users.IgnoreQueryFilters().Count(u => u.OrganizationId == o.Id), db.Devices.IgnoreQueryFilters().Count(d => d.OrganizationId == o.Id && !d.IsRevoked),
                db.Devices.IgnoreQueryFilters().Count(d => d.OrganizationId == o.Id && !o.IsSuspended && !d.IsRevoked && d.LastSeenAt >= onlineSince),
                db.Devices.IgnoreQueryFilters().Where(d => d.OrganizationId == o.Id && !d.IsRevoked).Max(d => (DateTimeOffset?)d.LastSeenAt), o.CreatedAt)).ToListAsync(ct);
    }

    public async Task<CompanyDetailView?> GetCompanyDetailsAsync(Guid id, CancellationToken ct)
    {
        var company = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id && o.Code != PlatformIdentity.OrganizationCode, ct);
        if (company is null) return null;

        var onlineSince = DateTimeOffset.UtcNow.AddMinutes(-2);

        var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.OrganizationId == id)
            .OrderBy(u => u.Role == Roles.Admin ? 0 : u.Role == Roles.Technician ? 1 : 2)
            .ThenBy(u => u.DisplayName)
            .Select(u => new CompanyUserItem(u.Id, u.Email, u.DisplayName, u.Role, u.Status, u.CreatedAt))
            .ToListAsync(ct);

        var devices = await (
            from d in db.Devices.IgnoreQueryFilters().AsNoTracking().Where(d => d.OrganizationId == id)
            join u in db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.OrganizationId == id) on d.AssignedUserId equals u.Id into userGroup
            from u in userGroup.DefaultIfEmpty()
            orderby d.Name
            select new CompanyDeviceItem(
                d.Id,
                d.Name,
                d.OsVersion,
                d.AgentVersion,
                d.LastSeenAt,
                !company.IsSuspended && !d.IsRevoked && d.LastSeenAt >= onlineSince,
                d.IsRevoked,
                u != null ? u.DisplayName : null,
                d.AssetType,
                d.SerialNumber)
        ).ToListAsync(ct);

        var alerts = await (
            from a in db.Alerts.IgnoreQueryFilters().AsNoTracking().Where(a => a.OrganizationId == id)
            join d in db.Devices.IgnoreQueryFilters().AsNoTracking().Where(d => d.OrganizationId == id) on a.DeviceId equals d.Id into devGroup
            from d in devGroup.DefaultIfEmpty()
            orderby a.IsOpen descending, a.CreatedAt descending
            select new CompanyAlertItem(
                a.Id,
                a.Severity,
                a.Message,
                d != null ? d.Name : null,
                a.IsOpen,
                a.CreatedAt)
        ).Take(20).ToListAsync(ct);

        var audits = await db.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.OrganizationId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(25)
            .Select(a => new CompanyAuditItem(a.Id, a.Action, a.Reason, a.Outcome, a.CreatedAt))
            .ToListAsync(ct);

        return new CompanyDetailView(
            company.Id,
            company.Code,
            company.Name,
            company.IsSuspended,
            company.CreatedAt,
            company.UpdatedAt,
            users,
            devices,
            alerts,
            audits);
    }

    public async Task<PlatformSystemStatus> GetSystemStatusAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dbConnected = await db.Database.CanConnectAsync(ct);
        sw.Stop();
        var latencyMs = sw.Elapsed.TotalMilliseconds;

        var onlineSince = DateTimeOffset.UtcNow.AddMinutes(-2);

        var totalCompanies = await db.Organizations.CountAsync(o => o.Code != PlatformIdentity.OrganizationCode, ct);
        var activeCompanies = await db.Organizations.CountAsync(o => o.Code != PlatformIdentity.OrganizationCode && !o.IsSuspended, ct);
        var suspendedCompanies = totalCompanies - activeCompanies;

        var totalDevices = await db.Devices.IgnoreQueryFilters().CountAsync(d => !d.IsRevoked, ct);
        var onlineDevices = await (
            from d in db.Devices.IgnoreQueryFilters().AsNoTracking().Where(d => !d.IsRevoked && d.LastSeenAt >= onlineSince)
            join o in db.Organizations.AsNoTracking() on d.OrganizationId equals o.Id
            where !o.IsSuspended
            select d.Id
        ).CountAsync(ct);

        var totalUsers = await db.Users.IgnoreQueryFilters().CountAsync(u => u.Role != Roles.PlatformOwner, ct);

        var vpsNodes = await db.VpsNodes.AsNoTracking()
            .OrderBy(n => n.Name)
            .Select(n => new VpsNodeHealthItem(
                n.Id,
                n.Name,
                n.Host,
                n.Port,
                n.Username,
                n.HostKeyFingerprint ?? "Chưa ghim",
                n.Status,
                n.ErrorMessage,
                n.LastCheckedAt,
                n.CpuPercent,
                n.RamPercent,
                n.DiskPercent))
            .ToListAsync(ct);

        var process = System.Diagnostics.Process.GetCurrentProcess();
        var uptimeSeconds = (DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds;
        var memoryMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2);

        return new PlatformSystemStatus(
            db.Database.ProviderName ?? "Unknown",
            dbConnected,
            Math.Round(latencyMs, 2),
            Environment.MachineName,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            uptimeSeconds,
            memoryMb,
            totalCompanies,
            activeCompanies,
            suspendedCompanies,
            totalDevices,
            onlineDevices,
            totalUsers,
            vpsNodes);
    }

    public Task<Organization?> FindAsync(Guid id, CancellationToken ct) => db.Organizations.SingleOrDefaultAsync(o => o.Id == id, ct);
    public Task<bool> ExistsAsync(string code, string name, CancellationToken ct) => db.Organizations.AnyAsync(o => o.Code == code || o.Name == name, ct);
    public Task<bool> NonceExistsAsync(Guid actorId, Guid nonce, CancellationToken ct) => db.PlatformOperations.AnyAsync(o => o.ActorId == actorId && o.Nonce == nonce, ct);
    public async Task RevokeCompanySessionsAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var user in await db.Users.IgnoreQueryFilters().Where(u => u.OrganizationId == id).ToListAsync(ct)) user.SecurityStamp = Guid.NewGuid().ToString("N");
        foreach (var session in await db.RefreshSessions.IgnoreQueryFilters().Where(s => s.OrganizationId == id && s.RevokedAt == null).ToListAsync(ct)) session.TryRevoke(now, "Company suspended");
    }
    public void AddCompany(Organization organization, User admin, AccountActivationToken invitation) { db.Add(organization); db.Add(admin); db.Add(invitation); }
    public Task<User?> FindPendingAdminAsync(Guid id, CancellationToken ct) => db.Users.IgnoreQueryFilters().Where(u => u.OrganizationId == id && u.Role == Roles.Admin && u.Status == UserStatuses.PendingActivation).OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(ct);
    public async Task ReplaceInvitationAsync(AccountActivationToken invitation, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var token in await db.AccountActivationTokens.IgnoreQueryFilters().Where(t => t.OrganizationId == invitation.OrganizationId && t.UserId == invitation.UserId && t.RevokedAt == null && t.UsedAt == null).ToListAsync(ct)) token.Revoke(now);
        db.Add(invitation);
    }
    public void AddOperation(PlatformOperation operation, AuditLog audit) { db.Add(operation); db.Add(audit); }
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException) { return false; }
    }
}
