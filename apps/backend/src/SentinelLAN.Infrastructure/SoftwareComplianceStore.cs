using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class SoftwareCatalogDetectionReader(SentinelDbContext db) : ISoftwareCatalogDetectionReader
{
    public async Task<IReadOnlyList<SoftwareDetectionRuleDto>> GetRulesAsync(Guid org, CancellationToken ct) =>
        await db.Set<SelfServiceCatalogApp>().AsNoTracking().Where(value => value.OrganizationId == org && value.IsActive)
            .Select(value => new SoftwareDetectionRuleDto(value.Id, value.Name, "Catalog", value.ProductCode, value.UpgradeCode, value.Version, value.DetectionRevision)).ToListAsync(ct);
    public async Task<IReadOnlyList<Guid>> GetKnownIdsAsync(Guid org, CancellationToken ct) =>
        await db.Set<SelfServiceCatalogApp>().Where(value => value.OrganizationId == org).Select(value => value.Id).ToListAsync(ct);
}

public sealed class SoftwareComplianceStore(SentinelDbContext db) : ISoftwareComplianceStore
{
    public async Task<IReadOnlyList<ProhibitedSoftwareRule>> GetProhibitedRulesAsync(Guid org, CancellationToken ct) =>
        await db.Set<ProhibitedSoftwareRule>().Where(value => value.OrganizationId == org).ToListAsync(ct);
    public Task<ProhibitedSoftwareRule?> FindRuleAsync(Guid org, Guid id, CancellationToken ct) =>
        db.Set<ProhibitedSoftwareRule>().SingleOrDefaultAsync(value => value.OrganizationId == org && value.Id == id, ct);
    public async Task<IReadOnlyList<DeviceSoftwareObservation>> GetObservationsAsync(Guid org, Guid device, CancellationToken ct) =>
        await db.Set<DeviceSoftwareObservation>().Where(value => value.OrganizationId == org && value.DeviceId == device).ToListAsync(ct);
    public void Add(ProhibitedSoftwareRule rule) => db.Add(rule);
    public void Add(DeviceSoftwareObservation observation) => db.Add(observation);
}

public sealed class SoftwareDetectionQuota(SentinelDbContext db) : ISoftwareDetectionQuota
{
    public async Task<bool> CanUseAsync(Guid org, Guid id, string source, bool enabled, CancellationToken ct)
    {
        var approved = await db.Set<SelfServiceCatalogApp>().CountAsync(value => value.OrganizationId == org && value.IsActive &&
            (value.ProductCode != null || value.UpgradeCode != null) && (source != "Catalog" || value.Id != id), ct);
        var prohibited = await db.Set<ProhibitedSoftwareRule>().CountAsync(value => value.OrganizationId == org && value.IsActive &&
            (source != "Prohibited" || value.Id != id), ct);
        return approved + prohibited + (enabled ? 1 : 0) <= 256;
    }
}
