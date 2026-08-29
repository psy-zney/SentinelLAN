using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class AuthenticationStore(SentinelDbContext db) : IAuthenticationStore
{
    public Task<User?> FindUserAsync(string organizationCode, string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users
            .Join(
                db.Organizations.Where(organization => organization.Code == organizationCode),
                user => user.OrganizationId,
                organization => organization.Id,
                (user, _) => user)
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindUserAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == userId && user.OrganizationId == organizationId, cancellationToken);

    public Task<RefreshSession?> FindRefreshSessionAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshSessions.SingleOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshSession>> FindRefreshFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        await db.RefreshSessions.Where(session => session.FamilyId == familyId).ToListAsync(cancellationToken);

    public void Add(RefreshSession session) => db.RefreshSessions.Add(session);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
