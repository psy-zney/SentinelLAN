using System.Security.Cryptography;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class RsaCommandSecurityTests
{
    [Fact]
    public void AgentVerifiesServerSignatureAndRejectsChangesToEverySignedField()
    {
        using var key = RSA.Create(3072);
        using var signer = new RsaCommandSigner(key.ExportPkcs8PrivateKeyPem(), "company-2026");
        var verifier = new RsaCommandVerifier(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), "company-2026");
        var now = DateTimeOffset.UtcNow;
        var command = new DeviceCommand
        {
            OrganizationId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IssuedByUserId = Guid.NewGuid(),
            Type = "ShowNotification", Reason = "Kiểm tra máy công ty", Nonce = Guid.NewGuid().ToString("N"), Signature = "",
            Parameter = "Thông báo đã duyệt", IssuedAt = now, ExpiresAt = now.AddMinutes(5)
        };
        command.Signature = signer.Sign(command);
        Assert.True(signer.Verify(command));
        var remote = new RemoteCommand(command.Id, command.DeviceId, command.Type, command.Reason,
            command.Nonce, command.Signature, command.IssuedAt, command.ExpiresAt,
            command.OrganizationId, command.IssuedByUserId, command.Parameter);
        Assert.True(verifier.Verify(remote));
        RemoteCommand[] tampered =
        [
            remote with { Id = Guid.NewGuid() }, remote with { DeviceId = Guid.NewGuid() },
            remote with { OrganizationId = Guid.NewGuid() }, remote with { IssuedByUserId = Guid.NewGuid() },
            remote with { Type = "LockWorkstation" }, remote with { Reason = "changed" },
            remote with { Parameter = "changed" }, remote with { Nonce = "changed" },
            remote with { IssuedAt = now.AddSeconds(1) }, remote with { ExpiresAt = now.AddMinutes(6) },
            remote with { Signature = remote.Signature.Replace("company-2026", "other-key", StringComparison.Ordinal) },
            remote with { Signature = remote.Signature.Replace("R2.", "R3.", StringComparison.Ordinal) },
            remote with { Signature = "not-a-signature" }
        ];
        Assert.All(tampered, item => Assert.False(verifier.Verify(item)));
        var gate = new CommandVerifier();
        Assert.True(gate.TryAccept(remote, remote.DeviceId, now, verifier.Verify, out _));
        Assert.False(gate.TryAccept(remote, remote.DeviceId, now, verifier.Verify, out var reason));
        Assert.Contains("Replay", reason, StringComparison.Ordinal);
        Assert.False(new CommandVerifier().TryAccept(remote, Guid.NewGuid(), now, verifier.Verify, out _));
        Assert.False(new CommandVerifier().TryAccept(remote, remote.DeviceId, remote.ExpiresAt, verifier.Verify, out _));
        using var wrongKey = RSA.Create(3072);
        Assert.False(new RsaCommandVerifier(Convert.ToBase64String(wrongKey.ExportSubjectPublicKeyInfo()), "company-2026").Verify(remote));
        Assert.False(verifier.Verify(remote with { Signature = new HmacCommandSigner("old-shared-key").Sign(command) }));
    }

    [Fact]
    public void WeakKeysAndPublicOnlySigningKeysFailConfiguration()
    {
        using var weak = RSA.Create(2048);
        Assert.Throws<ArgumentException>(() => new RsaCommandSigner(weak.ExportPkcs8PrivateKeyPem(), "key"));
        Assert.Throws<ArgumentException>(() => new RsaCommandVerifier(Convert.ToBase64String(weak.ExportSubjectPublicKeyInfo()), "key"));
        using var key = RSA.Create(3072);
        Assert.ThrowsAny<CryptographicException>(() => new RsaCommandSigner(key.ExportSubjectPublicKeyInfoPem(), "key"));
        Assert.Throws<ArgumentException>(() => new RsaCommandSigner(key.ExportPkcs8PrivateKeyPem(), "bad.key"));
    }
}
