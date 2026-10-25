using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class AuthenticationService(
    IAuthenticationStore store,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokens,
    IRefreshTokenProtector refreshTokens,
    AuthenticationSettings settings,
    TimeProvider timeProvider,
    ILoginAttemptLimiter? loginAttempts = null)
{
    private const string DummyPasswordHash = "pbkdf2-sha256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private string? ResolveOrganizationCode(string? requestedCode)
    {
        var configuredCode = settings.OrganizationCode?.Trim().ToLowerInvariant();
        var requested = requestedCode?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(configuredCode) && !string.IsNullOrEmpty(requested) && requested != configuredCode)
            return null;
        return configuredCode ?? requested;
    }

    private async Task<User?> VerifyLoginAsync(string organizationCode, string email, string password, CancellationToken ct, bool mobile = false)
    {
        if (loginAttempts is not null && await loginAttempts.ReserveAsync(organizationCode, email, ct) is { } retryAfter)
            throw new LoginThrottledException(retryAfter);
        var user = await store.FindUserAsync(organizationCode, email, ct);
        var active = user is { Status: UserStatuses.Active, Role: Roles.Admin or Roles.Employee } &&
            (await store.FindOrganizationAsync(user.OrganizationId, ct)) is { IsSuspended: false };
        var verification = passwordHasher.Verify(password, active ? user!.PasswordHash : DummyPasswordHash);
        if (!active || verification == PasswordVerificationResult.Failed)
        {
            if (user is not null)
            {
                store.AddAudit(new AuditLog
                {
                    OrganizationId = user.OrganizationId,
                    ActorId = user.Id,
                    Action = mobile ? "MobileLoginFailed" : "LoginFailed",
                    Reason = "Invalid credentials or inactive account",
                    Outcome = "Failure"
                });
                await store.SaveChangesAsync(ct);
            }
            return null;
        }
        if (verification == PasswordVerificationResult.SuccessNeedsRehash) user!.PasswordHash = passwordHasher.Hash(password);
        user!.SecurityStamp ??= Guid.NewGuid().ToString("N");
        return user;
    }
    public async Task<AuthenticationResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var organizationCode = ResolveOrganizationCode(request.OrganizationCode);
        if (string.IsNullOrWhiteSpace(organizationCode) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024) return null;
        var email = request.Email.Trim().ToLowerInvariant();
        if (organizationCode.Length is < 2 or > 64 || email.Length is < 3 or > 320) return null;

        var user = await VerifyLoginAsync(organizationCode, email, request.Password, cancellationToken);
        if (user is null) return null;

        var now = timeProvider.GetUtcNow();
        var refreshToken = refreshTokens.Generate();
        var refreshSession = CreateRefreshSession(user, Guid.NewGuid(), refreshToken, now);
        store.Add(refreshSession);
        await store.SaveChangesAsync(cancellationToken);
        if (loginAttempts is not null) await loginAttempts.ResetAsync(organizationCode, email, cancellationToken);
        return CreateResult(user, refreshSession, refreshToken, now);
    }

    public async Task<MobileAuthenticationResult?> LoginMobileAsync(MobileLoginRequest request, CancellationToken cancellationToken)
    {
        var organizationCode = ResolveOrganizationCode(request.OrganizationCode);
        if (string.IsNullOrWhiteSpace(organizationCode) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024) return null;
        var email = request.Email.Trim().ToLowerInvariant();
        if (organizationCode.Length is < 2 or > 64 || email.Length is < 3 or > 320) return null;

        var user = await VerifyLoginAsync(organizationCode, email, request.Password, cancellationToken, mobile: true);
        if (user is null) return null;

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
        if (loginAttempts is not null) await loginAttempts.ResetAsync(organizationCode, email, cancellationToken);
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
        if (user is null || user.Status != UserStatuses.Active || current.SecurityStamp is null || current.SecurityStamp != user.SecurityStamp)
            return new RefreshResult(RefreshStatus.Invalid);
        if (user.Role is not (Roles.Admin or Roles.Employee) || (await store.FindOrganizationAsync(user.OrganizationId, cancellationToken))?.IsSuspended == true)
            return new RefreshResult(RefreshStatus.Invalid);

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
        if (user is null || user.Status != UserStatuses.Active || current.SecurityStamp is null || current.SecurityStamp != user.SecurityStamp)
            return new MobileRefreshResult(RefreshStatus.Invalid);
        if (user.Role is not (Roles.Admin or Roles.Employee) || (await store.FindOrganizationAsync(user.OrganizationId, cancellationToken))?.IsSuspended == true)
            return new MobileRefreshResult(RefreshStatus.Invalid);

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
        var user = await store.FindUserAsync(userId, organizationId, cancellationToken);
        if (user is null) return;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = now;
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
        SecurityStamp = user.SecurityStamp,
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
