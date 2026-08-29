using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class AuthenticationService(
    IAuthenticationStore store,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokens,
    IRefreshTokenProtector refreshTokens,
    AuthenticationSettings settings,
    TimeProvider timeProvider)
{
    public async Task<AuthenticationResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationCode) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024) return null;
        var organizationCode = request.OrganizationCode.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        if (organizationCode.Length is < 2 or > 64 || email.Length is < 3 or > 320) return null;

        var user = await store.FindUserAsync(organizationCode, email, cancellationToken);
        if (user is null || user.Role is not (Roles.Admin or Roles.Technician or Roles.Employee)) return null;

        var verification = passwordHasher.Verify(request.Password, user.PasswordHash);
        if (verification == PasswordVerificationResult.Failed) return null;
        if (verification == PasswordVerificationResult.SuccessNeedsRehash) user.PasswordHash = passwordHasher.Hash(request.Password);

        var now = timeProvider.GetUtcNow();
        var refreshToken = refreshTokens.Generate();
        var refreshSession = CreateRefreshSession(user, Guid.NewGuid(), refreshToken, now);
        store.Add(refreshSession);
        await store.SaveChangesAsync(cancellationToken);
        return CreateResult(user, refreshSession, refreshToken, now);
    }

    public async Task<RefreshResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return new RefreshResult(RefreshStatus.Invalid);

        var current = await store.FindRefreshSessionAsync(refreshTokens.Hash(refreshToken), cancellationToken);
        if (current is null) return new RefreshResult(RefreshStatus.Invalid);

        var now = timeProvider.GetUtcNow();
        if (current.ReplacedBySessionId is not null)
        {
            await RevokeFamilyAsync(current.FamilyId, now, "Refresh token reuse detected", cancellationToken);
            return new RefreshResult(RefreshStatus.ReuseDetected);
        }

        if (!current.IsActive(now)) return new RefreshResult(RefreshStatus.Invalid);
        var user = await store.FindUserAsync(current.UserId, current.OrganizationId, cancellationToken);
        if (user is null) return new RefreshResult(RefreshStatus.Invalid);

        var replacementToken = refreshTokens.Generate();
        var replacement = CreateRefreshSession(user, current.FamilyId, replacementToken, now);
        if (!current.TryRotate(now, replacement.Id)) return new RefreshResult(RefreshStatus.Invalid);
        store.Add(replacement);
        if (!await store.TrySaveChangesAsync(cancellationToken)) return new RefreshResult(RefreshStatus.Invalid);

        return new RefreshResult(RefreshStatus.Succeeded, CreateResult(user, replacement, replacementToken, now));
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var session = await store.FindRefreshSessionAsync(refreshTokens.Hash(refreshToken), cancellationToken);
        if (session is null || !session.TryRevoke(timeProvider.GetUtcNow(), "Signed out")) return;
        await store.SaveChangesAsync(cancellationToken);
    }

    private RefreshSession CreateRefreshSession(User user, Guid familyId, string token, DateTimeOffset now) => new()
    {
        OrganizationId = user.OrganizationId,
        UserId = user.Id,
        FamilyId = familyId,
        TokenHash = refreshTokens.Hash(token),
        ExpiresAt = now.Add(settings.RefreshTokenLifetime),
        CreatedAt = now,
        UpdatedAt = now
    };

    private AuthenticationResult CreateResult(User user, RefreshSession refreshSession, string refreshToken, DateTimeOffset now)
    {
        var accessExpiresAt = now.Add(settings.AccessTokenLifetime);
        return new AuthenticationResult(
            accessTokens.Create(user, now, accessExpiresAt),
            accessExpiresAt,
            refreshToken,
            refreshSession.ExpiresAt,
            user.Role,
            user.DisplayName);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var family = await store.FindRefreshFamilyAsync(familyId, cancellationToken);
        foreach (var session in family) session.TryRevoke(now, reason);
        await store.SaveChangesAsync(cancellationToken);
    }
}
