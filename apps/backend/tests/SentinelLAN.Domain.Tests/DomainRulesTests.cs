using SentinelLAN.Domain;

namespace SentinelLAN.Domain.Tests;

public sealed class DomainRulesTests
{
    [Fact]
    public void EnrollmentTokenIsSingleUseAndExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new DeviceEnrollmentToken { OrganizationId = Guid.NewGuid(), TokenHash = "hash", ExpiresAt = now.AddMinutes(1) };
        Assert.True(token.TryUse(now));
        Assert.False(token.TryUse(now.AddSeconds(1)));
        var expired = new DeviceEnrollmentToken { OrganizationId = Guid.NewGuid(), TokenHash = "hash2", ExpiresAt = now };
        Assert.False(expired.TryUse(now));
    }

    [Fact]
    public void OnlySafeCommandTypesAreDeliverableBeforeExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        DeviceCommand Build(string type, DateTimeOffset expiry) => new() { OrganizationId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IssuedByUserId = Guid.NewGuid(), Type = type, Reason = "Authorized demo", Nonce = Guid.NewGuid().ToString("N"), Signature = "signature", IssuedAt = now, ExpiresAt = expiry, Status = DeviceCommandStatus.Pending };
        Assert.True(Build("SimulateLock", now.AddMinutes(1)).CanDeliver(now));
        Assert.False(Build("ExecutePowerShell", now.AddMinutes(1)).CanDeliver(now));
        Assert.False(Build("ShowNotification", now).CanDeliver(now));
    }

    [Fact]
    public void RefreshSessionRotatesOnceAndCanBeRevoked()
    {
        var now = DateTimeOffset.UtcNow;
        var replacementId = Guid.NewGuid();
        var session = new RefreshSession
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash",
            ExpiresAt = now.AddDays(1)
        };

        Assert.True(session.TryRotate(now, replacementId));
        Assert.Equal(replacementId, session.ReplacedBySessionId);
        Assert.False(session.TryRotate(now.AddSeconds(1), Guid.NewGuid()));
        Assert.False(session.IsActive(now.AddSeconds(1)));

        var active = new RefreshSession
        {
            OrganizationId = session.OrganizationId,
            UserId = session.UserId,
            FamilyId = session.FamilyId,
            TokenHash = "replacement",
            ExpiresAt = now.AddDays(1)
        };
        Assert.True(active.TryRevoke(now, "Signed out"));
        Assert.False(active.TryRevoke(now.AddSeconds(1), "Duplicate"));
    }
}
