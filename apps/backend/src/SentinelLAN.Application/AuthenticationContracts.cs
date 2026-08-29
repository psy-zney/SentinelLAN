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
    Task<RefreshSession?> FindRefreshSessionAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshSession>> FindRefreshFamilyAsync(Guid familyId, CancellationToken cancellationToken);
    void Add(RefreshSession session);
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
