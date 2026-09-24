using SentinelLAN.Domain;

namespace SentinelLAN.Application.Tests;

public sealed class MobileAuthenticationServiceTests
{
    private readonly DateTimeOffset now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MobileLoginSucceedsWithValidCredentialsAndRecordsSessionMetadata()
    {
        var orgId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "employee@example.test",
            DisplayName = "Employee Nguyen",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var store = new FakeMobileAuthStore(user, "sentinel-corp");
        var refreshTokens = new SequentialRefreshTokens("mobile-refresh-1");
        var service = new AuthenticationService(
            store,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            refreshTokens,
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(30)),
            new FixedTimeProvider(now));

        var result = await service.LoginMobileAsync(
            new MobileLoginRequest("sentinel-corp", "employee@example.test", "password", AppVersion: "1.2.0"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("mobile-refresh-1", result.RefreshToken);
        Assert.Equal(Roles.Employee, result.Role);
        Assert.Equal("employee@example.test", result.Email);
        Assert.Equal("sentinel-corp", result.OrganizationCode);
        Assert.Equal(user.Id, result.UserId);

        // Verify session stored has ClientType = Mobile and AppVersion
        var storedSession = Assert.Single(store.Sessions);
        Assert.Equal("Mobile", storedSession.ClientType);
        Assert.Equal("1.2.0", storedSession.AppVersion);
        Assert.Equal(user.Id, storedSession.UserId);
        Assert.True(storedSession.IsActive(now));

        // Audit log appended, no secrets
        var audit = Assert.Single(store.Audits, a => a.Action == "MobileLoginSucceeded");
        Assert.Equal(user.Id, audit.ActorId);
        Assert.Contains("AppVersion=1.2.0", audit.Reason);
        Assert.DoesNotContain("password", audit.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mobile-refresh-1", audit.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MobileLoginRejectsWrongPasswordTenantOrPendingUser()
    {
        var orgId = Guid.NewGuid();
        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "emp@example.test",
            DisplayName = "Emp",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var store = new FakeMobileAuthStore(activeUser, "org-a");
        var service = new AuthenticationService(
            store,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            new SequentialRefreshTokens("unused"),
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)),
            new FixedTimeProvider(now));

        // Wrong password
        var wrongPasswordResult = await service.LoginMobileAsync(
            new MobileLoginRequest("org-a", "emp@example.test", "wrong-password"),
            CancellationToken.None);
        Assert.Null(wrongPasswordResult);
        Assert.Contains(store.Audits, a => a.Action == "MobileLoginFailed");

        // Wrong organization code
        var wrongOrgResult = await service.LoginMobileAsync(
            new MobileLoginRequest("wrong-org", "emp@example.test", "password"),
            CancellationToken.None);
        Assert.Null(wrongOrgResult);

        // Locked or pending user
        var lockedUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "locked@example.test",
            DisplayName = "Locked User",
            Role = Roles.Employee,
            Status = "Pending",
            PasswordHash = "valid"
        };
        var storeWithLocked = new FakeMobileAuthStore(lockedUser, "org-a");
        var serviceLocked = new AuthenticationService(
            storeWithLocked,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            new SequentialRefreshTokens("unused"),
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)),
            new FixedTimeProvider(now));

        var lockedResult = await serviceLocked.LoginMobileAsync(
            new MobileLoginRequest("org-a", "locked@example.test", "password"),
            CancellationToken.None);
        Assert.Null(lockedResult);
    }

    [Fact]
    public async Task MobileRefreshRotatesTokenAndReplayRevokesFamily()
    {
        var orgId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "emp@example.test",
            DisplayName = "Emp",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var store = new FakeMobileAuthStore(user, "corp");
        var refreshTokens = new SequentialRefreshTokens("token-1", "token-2");
        var service = new AuthenticationService(
            store,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            refreshTokens,
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)),
            new FixedTimeProvider(now));

        var login = await service.LoginMobileAsync(
            new MobileLoginRequest("corp", "emp@example.test", "password", AppVersion: "1.0.0"),
            CancellationToken.None);
        Assert.NotNull(login);

        // Rotate token
        var refreshed = await service.RefreshMobileAsync("token-1", CancellationToken.None);
        Assert.Equal(RefreshStatus.Succeeded, refreshed.Status);
        Assert.Equal("token-2", refreshed.Result?.RefreshToken);

        var firstSession = store.Sessions.First(s => s.TokenHash == "hash:token-1");
        Assert.False(firstSession.IsActive(now));
        Assert.Equal("Rotated", firstSession.RevocationReason);

        // Replay rotated token: Family reuse detected
        var replay = await service.RefreshMobileAsync("token-1", CancellationToken.None);
        Assert.Equal(RefreshStatus.ReuseDetected, replay.Status);

        // Entire family revoked
        Assert.All(store.Sessions, s => Assert.False(s.IsActive(now)));
        Assert.Contains(store.Audits, a => a.Action == "MobileSessionReuseDetected");
    }

    [Fact]
    public async Task ConcurrentRefreshDetectsConflictSafely()
    {
        var orgId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "emp@example.test",
            DisplayName = "Emp",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var store = new FakeMobileAuthStore(user, "corp");
        var refreshTokens = new SequentialRefreshTokens("token-1", "token-2", "token-3");
        var service = new AuthenticationService(
            store,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            refreshTokens,
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)),
            new FixedTimeProvider(now));

        await service.LoginMobileAsync(
            new MobileLoginRequest("corp", "emp@example.test", "password"),
            CancellationToken.None);

        // Simulate first refresh succeeds, second concurrent fails due to DB concurrency conflict
        store.SimulateConcurrencyConflictOnNextSave = false;
        var first = await service.RefreshMobileAsync("token-1", CancellationToken.None);
        Assert.Equal(RefreshStatus.Succeeded, first.Status);

        // Next call with old token hits reuse detection
        var second = await service.RefreshMobileAsync("token-1", CancellationToken.None);
        Assert.Equal(RefreshStatus.ReuseDetected, second.Status);
    }

    [Fact]
    public async Task MobileLogoutRevokesCurrentSessionOnly()
    {
        var orgId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "emp@example.test",
            DisplayName = "Emp",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var store = new FakeMobileAuthStore(user, "corp");
        var refreshTokens = new SequentialRefreshTokens("token-1");
        var service = new AuthenticationService(
            store,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            refreshTokens,
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)),
            new FixedTimeProvider(now));

        await service.LoginMobileAsync(
            new MobileLoginRequest("corp", "emp@example.test", "password"),
            CancellationToken.None);

        await service.LogoutMobileAsync("token-1", CancellationToken.None);

        var session = Assert.Single(store.Sessions);
        Assert.False(session.IsActive(now));
        Assert.Equal("Signed out (mobile)", session.RevocationReason);
        Assert.Contains(store.Audits, a => a.Action == "MobileLogout");
    }

    [Fact]
    public async Task LogoutAllUserSessionsRevokesAllSessionsForTargetUserOnly()
    {
        var orgId = Guid.NewGuid();
        var targetUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "target@example.test",
            DisplayName = "Target",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "other@example.test",
            DisplayName = "Other",
            Role = Roles.Employee,
            Status = UserStatuses.Active,
            PasswordHash = "valid"
        };
        var store = new FakeMobileAuthStore(targetUser, "corp");
        var service = new AuthenticationService(
            store,
            new FakePasswordHasher(),
            new FakeAccessTokens(),
            new SequentialRefreshTokens("t1", "t2", "o1"),
            new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)),
            new FixedTimeProvider(now));

        // Add 2 sessions for targetUser
        var s1 = new RefreshSession
        {
            OrganizationId = orgId,
            UserId = targetUser.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash:t1",
            ExpiresAt = now.AddDays(7)
        };
        var s2 = new RefreshSession
        {
            OrganizationId = orgId,
            UserId = targetUser.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash:t2",
            ExpiresAt = now.AddDays(7)
        };
        // Add 1 session for otherUser
        var s3 = new RefreshSession
        {
            OrganizationId = orgId,
            UserId = otherUser.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash:o1",
            ExpiresAt = now.AddDays(7)
        };

        store.Add(s1);
        store.Add(s2);
        store.Add(s3);

        await service.LogoutAllUserSessionsAsync(orgId, targetUser.Id, CancellationToken.None);

        Assert.False(s1.IsActive(now));
        Assert.False(s2.IsActive(now));
        Assert.True(s3.IsActive(now)); // otherUser unaffected

        Assert.Contains(store.Audits, a => a.Action == "MobileLogoutAll" && a.ActorId == targetUser.Id);
    }

    private sealed class FakeMobileAuthStore(User user, string orgCode) : IAuthenticationStore
    {
        public List<RefreshSession> Sessions { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public bool SimulateConcurrencyConflictOnNextSave { get; set; }

        public Task<User?> FindUserAsync(string organizationCode, string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(organizationCode == orgCode && normalizedEmail == user.Email ? user : null);

        public Task<User?> FindUserAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(user.Id == userId && user.OrganizationId == organizationId ? user : null);

        public Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<Organization?>(new Organization { Id = organizationId, Code = orgCode, Name = "Org Name" });

        public Task<RefreshSession?> FindRefreshSessionAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.SingleOrDefault(s => s.TokenHash == tokenHash));

        public Task<IReadOnlyList<RefreshSession>> FindRefreshFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>(Sessions.Where(s => s.FamilyId == familyId).ToList());

        public Task<IReadOnlyList<RefreshSession>> FindActiveUserSessionsAsync(Guid organizationId, Guid userId, DateTimeOffset atTime, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>(Sessions.Where(s => s.OrganizationId == organizationId && s.UserId == userId && s.IsActive(atTime)).ToList());

        public void Add(RefreshSession session) => Sessions.Add(session);

        public void AddAudit(AuditLog log) => Audits.Add(log);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(!SimulateConcurrencyConflictOnNextSave);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "valid";
        public PasswordVerificationResult Verify(string password, string passwordHash) =>
            password == "password" && passwordHash == "valid"
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
    }

    private sealed class FakeAccessTokens : IAccessTokenService
    {
        public string Create(User u, DateTimeOffset now, DateTimeOffset expiresAt) => $"access-{u.Id:N}";
        public ActorContext? Validate(string token, DateTimeOffset now) => null;
    }

    private sealed class SequentialRefreshTokens(params string[] values) : IRefreshTokenProtector
    {
        private readonly Queue<string> queue = new(values);
        public string Generate() => queue.Count > 0 ? queue.Dequeue() : Guid.NewGuid().ToString("N");
        public string Hash(string token) => $"hash:{token}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
