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
        if (user is null || user.Role is not (Roles.Admin or Roles.Technician or Roles.Employee) || user.Status != UserStatuses.Active) return null;

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

    public async Task<MobileAuthenticationResult?> LoginMobileAsync(MobileLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationCode) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024) return null;
        var organizationCode = request.OrganizationCode.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        if (organizationCode.Length is < 2 or > 64 || email.Length is < 3 or > 320) return null;

        var user = await store.FindUserAsync(organizationCode, email, cancellationToken);
        if (user is null || user.Role is not (Roles.Admin or Roles.Technician or Roles.Employee) || user.Status != UserStatuses.Active)
        {
            if (user is not null)
            {
                store.AddAudit(new AuditLog
                {
                    OrganizationId = user.OrganizationId,
                    ActorId = user.Id,
                    Action = "MobileLoginFailed",
                    Reason = "Invalid credentials or inactive status",
                    Outcome = "Failure"
                });
                await store.SaveChangesAsync(cancellationToken);
            }
            return null;
        }

        var verification = passwordHasher.Verify(request.Password, user.PasswordHash);
        if (verification == PasswordVerificationResult.Failed)
        {
            store.AddAudit(new AuditLog
            {
                OrganizationId = user.OrganizationId,
                ActorId = user.Id,
                Action = "MobileLoginFailed",
                Reason = "Invalid credentials",
                Outcome = "Failure"
            });
            await store.SaveChangesAsync(cancellationToken);
            return null;
        }
        if (verification == PasswordVerificationResult.SuccessNeedsRehash) user.PasswordHash = passwordHasher.Hash(request.Password);

        var org = await store.FindOrganizationAsync(user.OrganizationId, cancellationToken);
        var orgCode = org?.Code ?? organizationCode;

        var now = timeProvider.GetUtcNow();
        var refreshToken = refreshTokens.Generate();
        var refreshSession = CreateRefreshSession(user, Guid.NewGuid(), refreshToken, now, clientType: "Mobile", appVersion: request.AppVersion);
        store.Add(refreshSession);

        store.AddAudit(new AuditLog
        {
            OrganizationId = user.OrganizationId,
            ActorId = user.Id,
            Action = "MobileLoginSucceeded",
            Reason = $"ClientType=Mobile, AppVersion={request.AppVersion ?? "unknown"}",
            Outcome = "Success"
        });

        await store.SaveChangesAsync(cancellationToken);
        return CreateMobileResult(user, refreshSession, refreshToken, orgCode, now);
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
        if (user is null || user.Status != UserStatuses.Active) return new RefreshResult(RefreshStatus.Invalid);

        var replacementToken = refreshTokens.Generate();
        var replacement = CreateRefreshSession(user, current.FamilyId, replacementToken, now);
        if (!current.TryRotate(now, replacement.Id)) return new RefreshResult(RefreshStatus.Invalid);
        store.Add(replacement);
        if (!await store.TrySaveChangesAsync(cancellationToken)) return new RefreshResult(RefreshStatus.Invalid);

        return new RefreshResult(RefreshStatus.Succeeded, CreateResult(user, replacement, replacementToken, now));
    }

    public async Task<MobileRefreshResult> RefreshMobileAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return new MobileRefreshResult(RefreshStatus.Invalid);

        var current = await store.FindRefreshSessionAsync(refreshTokens.Hash(refreshToken), cancellationToken);
        if (current is null) return new MobileRefreshResult(RefreshStatus.Invalid);

        var now = timeProvider.GetUtcNow();
        if (current.ReplacedBySessionId is not null)
        {
            store.AddAudit(new AuditLog
            {
                OrganizationId = current.OrganizationId,
                ActorId = current.UserId,
                Action = "MobileSessionReuseDetected",
                Reason = $"Family {current.FamilyId} revoked due to reuse",
                Outcome = "Warning"
            });
            await RevokeFamilyAsync(current.FamilyId, now, "Refresh token reuse detected", cancellationToken);
            return new MobileRefreshResult(RefreshStatus.ReuseDetected);
        }

        if (!current.IsActive(now)) return new MobileRefreshResult(RefreshStatus.Invalid);
        var user = await store.FindUserAsync(current.UserId, current.OrganizationId, cancellationToken);
        if (user is null || user.Status != UserStatuses.Active) return new MobileRefreshResult(RefreshStatus.Invalid);

        var org = await store.FindOrganizationAsync(user.OrganizationId, cancellationToken);
        var orgCode = org?.Code ?? string.Empty;

        var replacementToken = refreshTokens.Generate();
        var replacement = CreateRefreshSession(user, current.FamilyId, replacementToken, now, clientType: "Mobile", appVersion: current.AppVersion);
        if (!current.TryRotate(now, replacement.Id)) return new MobileRefreshResult(RefreshStatus.Invalid);
        store.Add(replacement);

        store.AddAudit(new AuditLog
        {
            OrganizationId = current.OrganizationId,
            ActorId = current.UserId,
            Action = "MobileSessionRefreshed",
            Reason = $"Session rotated for family {replacement.FamilyId}",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken)) return new MobileRefreshResult(RefreshStatus.Invalid);

        return new MobileRefreshResult(RefreshStatus.Succeeded, CreateMobileResult(user, replacement, replacementToken, orgCode, now));
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var session = await store.FindRefreshSessionAsync(refreshTokens.Hash(refreshToken), cancellationToken);
        if (session is null || !session.TryRevoke(timeProvider.GetUtcNow(), "Signed out")) return;
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutMobileAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var now = timeProvider.GetUtcNow();
        var session = await store.FindRefreshSessionAsync(refreshTokens.Hash(refreshToken), cancellationToken);
        if (session is null || !session.TryRevoke(now, "Signed out (mobile)")) return;

        store.AddAudit(new AuditLog
        {
            OrganizationId = session.OrganizationId,
            ActorId = session.UserId,
            Action = "MobileLogout",
            Reason = "Signed out from mobile device",
            Outcome = "Success"
        });

        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllUserSessionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sessions = await store.FindActiveUserSessionsAsync(organizationId, userId, now, cancellationToken);
        foreach (var session in sessions)
        {
            session.TryRevoke(now, "Revoked by logout-all");
        }

        store.AddAudit(new AuditLog
        {
            OrganizationId = organizationId,
            ActorId = userId,
            Action = "MobileLogoutAll",
            Reason = "All active sessions revoked by user",
            Outcome = "Success"
        });

        await store.SaveChangesAsync(cancellationToken);
    }

    private RefreshSession CreateRefreshSession(User user, Guid familyId, string token, DateTimeOffset now, string? clientType = null, string? appVersion = null) => new()
    {
        OrganizationId = user.OrganizationId,
        UserId = user.Id,
        FamilyId = familyId,
        TokenHash = refreshTokens.Hash(token),
        ClientType = clientType,
        AppVersion = appVersion,
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

    private MobileAuthenticationResult CreateMobileResult(User user, RefreshSession refreshSession, string refreshToken, string orgCode, DateTimeOffset now)
    {
        var accessExpiresAt = now.Add(settings.AccessTokenLifetime);
        return new MobileAuthenticationResult(
            accessTokens.Create(user, now, accessExpiresAt),
            accessExpiresAt,
            refreshToken,
            refreshSession.ExpiresAt,
            user.Role,
            user.DisplayName,
            user.Id,
            user.Email,
            user.OrganizationId,
            orgCode);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var family = await store.FindRefreshFamilyAsync(familyId, cancellationToken);
        foreach (var session in family) session.TryRevoke(now, reason);
        await store.SaveChangesAsync(cancellationToken);
    }
}
