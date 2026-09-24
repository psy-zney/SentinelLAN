using SentinelLAN.Domain;

namespace SentinelLAN.Domain.Tests;

public sealed class DomainRulesTests
{
    [Fact]
    public void DeviceBecomesOfflineAtTwoMinutesAndRevocationOverridesHeartbeat()
    {
        var now = DateTimeOffset.UtcNow;
        var device = new Device { OrganizationId = Guid.NewGuid(), Name = "Test", OsVersion = "Windows", AgentVersion = "test" };
        Assert.False(device.IsOnline(now));
        device.LastSeenAt = now;
        Assert.True(device.IsOnline(now.AddSeconds(119)));
        Assert.False(device.IsOnline(now.AddMinutes(2)));
        device.IsRevoked = true;
        Assert.False(device.IsOnline(now));
    }
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
    public void CommandDeliveryLeaseAllowsSameEnvelopeRedeliveryOnlyAfterLeaseAndBeforeExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var command = new DeviceCommand
        {
            OrganizationId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IssuedByUserId = Guid.NewGuid(),
            Type = "SimulateLock", Reason = "Authorized demo", Nonce = "stable-nonce", Signature = "stable-signature",
            IssuedAt = now, ExpiresAt = now.AddMinutes(1), Status = DeviceCommandStatus.Pending
        };

        Assert.True(command.TryLeaseForDelivery(now, TimeSpan.FromSeconds(30)));
        Assert.Equal(DeviceCommandStatus.Delivered, command.Status);
        Assert.Equal(now.AddSeconds(30), command.DeliveryLeaseExpiresAt);
        Assert.False(command.CanDeliver(now.AddSeconds(29)));
        Assert.True(command.CanDeliver(now.AddSeconds(30)));
        Assert.True(command.TryLeaseForDelivery(now.AddSeconds(30), TimeSpan.FromSeconds(30)));
        Assert.Equal("stable-nonce", command.Nonce);
        Assert.Equal("stable-signature", command.Signature);
        Assert.Equal(now.AddMinutes(1), command.DeliveryLeaseExpiresAt);
        Assert.False(command.CanDeliver(now.AddMinutes(1)));
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
