using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task RefreshRotatesTokenAndReuseRevokesTheFamily()
    {
        var now = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
        var user = new User { OrganizationId = Guid.NewGuid(), Email = "admin@example.test", DisplayName = "Admin", Role = Roles.Admin, PasswordHash = "valid" };
        var store = new FakeAuthenticationStore(user);
        var refreshTokens = new SequentialRefreshTokens("login-token", "replacement-token");
        var service = new AuthenticationService(store, new FakePasswordHasher(), new FakeAccessTokens(), refreshTokens, new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)), new FixedTimeProvider(now));

        var login = await service.LoginAsync(new LoginRequest("demo", user.Email, "password"), CancellationToken.None);
        Assert.NotNull(login);

        var refreshed = await service.RefreshAsync("login-token", CancellationToken.None);
        Assert.Equal(RefreshStatus.Succeeded, refreshed.Status);
        Assert.Equal("replacement-token", refreshed.Session?.RefreshToken);

        var reused = await service.RefreshAsync("login-token", CancellationToken.None);
        Assert.Equal(RefreshStatus.ReuseDetected, reused.Status);
        Assert.All(store.Sessions, session => Assert.False(session.IsActive(now)));
    }

    [Fact]
    public async Task LoginRejectsWrongTenantAndPassword()
    {
        var user = new User { OrganizationId = Guid.NewGuid(), Email = "admin@example.test", DisplayName = "Admin", Role = Roles.Admin, PasswordHash = "valid" };
        var store = new FakeAuthenticationStore(user);
        var service = new AuthenticationService(store, new FakePasswordHasher(), new FakeAccessTokens(), new SequentialRefreshTokens("unused"), new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)), new FixedTimeProvider(DateTimeOffset.UtcNow));

        Assert.Null(await service.LoginAsync(new LoginRequest("other", user.Email, "password"), CancellationToken.None));
        Assert.Null(await service.LoginAsync(new LoginRequest("demo", user.Email, "wrong"), CancellationToken.None));
        Assert.Null(await service.LoginAsync(new LoginRequest(null!, user.Email, "password"), CancellationToken.None));
    }

    private sealed class FakeAuthenticationStore(User user) : IAuthenticationStore
    {
        public List<RefreshSession> Sessions { get; } = [];

        public Task<User?> FindUserAsync(string organizationCode, string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(organizationCode == "demo" && normalizedEmail == user.Email ? user : null);

        public Task<User?> FindUserAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(user.Id == userId && user.OrganizationId == organizationId ? user : null);

        public Task<RefreshSession?> FindRefreshSessionAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.SingleOrDefault(session => session.TokenHash == tokenHash));

        public Task<IReadOnlyList<RefreshSession>> FindRefreshFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>(Sessions.Where(session => session.FamilyId == familyId).ToList());

        public void Add(RefreshSession session) => Sessions.Add(session);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "valid";
        public PasswordVerificationResult Verify(string password, string passwordHash) => password == "password" && passwordHash == "valid" ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
    }

    private sealed class FakeAccessTokens : IAccessTokenService
    {
        public string Create(User user, DateTimeOffset now, DateTimeOffset expiresAt) => $"access-{user.Id:N}";
        public ActorContext? Validate(string token, DateTimeOffset now) => null;
    }

    private sealed class SequentialRefreshTokens(params string[] values) : IRefreshTokenProtector
    {
        private readonly Queue<string> values = new(values);
        public string Generate() => values.Dequeue();
        public string Hash(string token) => $"hash:{token}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
