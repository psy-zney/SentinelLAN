using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class RealtimeSessionValidator(SentinelDbContext db) : IRealtimeSessionValidator
{
    public Task<bool> IsActiveAsync(ActorContext actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (actor.ExpiresAt <= now || actor.ExpiresAt is null || actor.SecurityStamp is null || actor.Role is not (Roles.Admin))
            return Task.FromResult(false);
        // Revalidation can run during another tenant's HTTP request. Bind scope to the authenticated
        // connection, not the caller that triggered the sweep, while retaining explicit tenant checks.
        return db.Users.IgnoreQueryFilters().AsNoTracking().AnyAsync(u => u.Id == actor.UserId && u.OrganizationId == actor.OrganizationId &&
            u.Role == actor.Role && u.Status == UserStatuses.Active && u.SecurityStamp == actor.SecurityStamp &&
            db.Organizations.Any(o => o.Id == actor.OrganizationId && !o.IsSuspended), cancellationToken);
    }
}
