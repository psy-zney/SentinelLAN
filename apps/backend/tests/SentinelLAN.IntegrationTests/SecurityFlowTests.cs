using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class SecurityFlowTests
{
    [Fact]
    public void CommandSignatureDetectsTampering()
    {
        var now = DateTimeOffset.UtcNow;
        var signer = new HmacCommandSigner("test-key-at-least-local");
        var command = new DeviceCommand { OrganizationId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IssuedByUserId = Guid.NewGuid(), Type = "ShowNotification", Reason = "Test", Nonce = "unique", Signature = "pending", IssuedAt = now, ExpiresAt = now.AddMinutes(1), Status = DeviceCommandStatus.Pending };
        command.Signature = signer.Sign(command);
        Assert.True(signer.Verify(command));
        var tampered = new DeviceCommand { Id = command.Id, OrganizationId = command.OrganizationId, DeviceId = Guid.NewGuid(), IssuedByUserId = command.IssuedByUserId, Type = command.Type, Reason = command.Reason, Nonce = command.Nonce, Signature = command.Signature, IssuedAt = command.IssuedAt, ExpiresAt = command.ExpiresAt, Status = command.Status };
        Assert.False(signer.Verify(tampered));
    }

    [Fact]
    public void SecretHashComparisonAcceptsOnlyOriginalValue()
    {
        var hash = SecretHash.Create("one-time-token");
        Assert.True(SecretHash.Matches("one-time-token", hash));
        Assert.False(SecretHash.Matches("different-token", hash));
    }

    [Fact]
    public void PasswordHasherUsesSaltAndUpgradesLegacyHashes()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var first = hasher.Hash("correct-password");
        var second = hasher.Hash("correct-password");

        Assert.NotEqual(first, second);
        Assert.Equal(PasswordVerificationResult.Success, hasher.Verify("correct-password", first));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify("wrong-password", first));
        Assert.Equal(PasswordVerificationResult.SuccessNeedsRehash, hasher.Verify("legacy", SecretHash.Create("legacy")));
    }

    [Fact]
    public void AccessTokenRejectsExpiryAndTampering()
    {
        var now = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
        var user = new User { OrganizationId = Guid.NewGuid(), Email = "admin@example.test", DisplayName = "Admin", Role = Roles.Admin, PasswordHash = "unused" };
        var service = new AccessTokenService("test-access-signing-key-with-more-than-32-characters");
        var token = service.Create(user, now, now.AddMinutes(15));

        Assert.Equal(user.Id, service.Validate(token, now)?.UserId);
        Assert.Null(service.Validate(token, now.AddMinutes(15)));
        Assert.Null(service.Validate($"{token}tampered", now));
    }
}
