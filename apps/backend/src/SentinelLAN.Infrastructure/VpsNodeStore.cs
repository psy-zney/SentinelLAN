using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class VpsNodeStore(SentinelDbContext dbContext) : IVpsNodeStore
{
    public async Task<IReadOnlyList<VpsNode>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await dbContext.VpsNodes
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<VpsNode?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.VpsNodes
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
    }

    public async Task<VpsNode> CreateAsync(VpsNode node, CancellationToken cancellationToken = default)
    {
        dbContext.VpsNodes.Add(node);
        await dbContext.SaveChangesAsync(cancellationToken);
        return node;
    }

    public async Task<VpsNode> UpdateAsync(VpsNode node, CancellationToken cancellationToken = default)
    {
        node.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.VpsNodes.Update(node);
        await dbContext.SaveChangesAsync(cancellationToken);
        return node;
    }

    public async Task<bool> DeleteAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default)
    {
        var node = await dbContext.VpsNodes
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

        if (node is null) return false;

        dbContext.VpsNodes.Remove(node);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsNameAsync(Guid organizationId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.VpsNodes
            .Where(x => x.OrganizationId == organizationId);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        var normalized = name.ToUpperInvariant();
        return await query.AnyAsync(x => x.Name.ToUpper(System.Globalization.CultureInfo.InvariantCulture) == normalized, cancellationToken);
    }

    public async Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
