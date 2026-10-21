using System.Security.Cryptography;
using SentinelLAN.Infrastructure;
using Xunit;

namespace SentinelLAN.IntegrationTests;

public sealed class VpsVaultIntegrationTests
{
    [Fact]
    public void VaultEncryptDecryptRoundtripSucceeds()
    {
        var vault = new VpsVaultService("test-secret-vault-key-32-chars-long!");
        const string privateKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtz\n-----END OPENSSH PRIVATE KEY-----";

        var encrypted = vault.Encrypt(privateKey);
        Assert.NotEqual(privateKey, encrypted);

        var decrypted = vault.Decrypt(encrypted);
        Assert.Equal(privateKey, decrypted);
    }

    [Fact]
    public void VaultTamperedCiphertextThrowsCryptographicException()
    {
        var vault = new VpsVaultService("test-secret-vault-key-32-chars-long!");
        var encrypted = vault.Encrypt("my-secret-key");

        var bytes = Convert.FromBase64String(encrypted);
        bytes[^1] ^= 0xFF; // tamper with last byte
        var tampered = Convert.ToBase64String(bytes);

        Assert.Throws<CryptographicException>(() => vault.Decrypt(tampered));
    }
}
