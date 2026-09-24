using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessNeedsRehash
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string password, string passwordHash);
}

public interface IAccessTokenService
{
    string Create(User user, DateTimeOffset now, DateTimeOffset expiresAt);
    ActorContext? Validate(string token, DateTimeOffset now);
}

public interface IRefreshTokenProtector
{
    string Generate();
    string Hash(string token);
}

public interface IAuthenticationStore
{
    Task<User?> FindUserAsync(string organizationCode, string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> FindUserAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
    Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<RefreshSession?> FindRefreshSessionAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshSession>> FindRefreshFamilyAsync(Guid familyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshSession>> FindActiveUserSessionsAsync(Guid organizationId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(RefreshSession session);
    void AddAudit(AuditLog log);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record AuthenticationSettings(TimeSpan AccessTokenLifetime, TimeSpan RefreshTokenLifetime);

public sealed record AuthenticationResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string Role,
    string DisplayName);

public enum RefreshStatus
{
    Succeeded,
    Invalid,
    ReuseDetected
}

public sealed record RefreshResult(RefreshStatus Status, AuthenticationResult? Session = null);

public sealed record MobileLoginRequest(
    string OrganizationCode,
    string Email,
    string Password,
    string? AppVersion = null,
    string? ClientNonce = null);

public sealed record MobileRefreshRequest(string RefreshToken);
public sealed record MobileLogoutRequest(string RefreshToken);

public sealed record MobileUserInfo(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid OrganizationId,
    string OrganizationCode);

public sealed record MobileAuthSessionResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType,
    MobileUserInfo User);

public sealed record MobileRefreshResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType);

public sealed record MobileBootstrapResponse(
    string MinimumAppVersion,
    string LatestAppVersion,
    string PrivacyManifestVersion,
    bool MaintenanceMode,
    string SupportEmail,
    IReadOnlyList<string> SupportedAuthSchemes);

public sealed record MobileAuthenticationResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string Role,
    string DisplayName,
    Guid UserId,
    string Email,
    Guid OrganizationId,
    string OrganizationCode);

public sealed record MobileRefreshResult(RefreshStatus Status, MobileAuthenticationResult? Result = null);
