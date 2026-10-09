using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class SensitiveDataEncryptionTests
{
    [Fact]
    public void EncryptionUsesUniqueNoncesAndRejectsTamperingWrongKeyAndWrongPurpose()
    {
        var cipher = new SensitiveDataCipher(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var data = Encoding.UTF8.GetBytes("Ảnh và trao đổi riêng tư");
        var encrypted = cipher.EncryptBytes(data, "attachment.content");
        Assert.NotEqual(encrypted, cipher.EncryptBytes(data, "attachment.content"));
        Assert.Equal(data, cipher.DecryptBytes(encrypted, "attachment.content"));
        Assert.ThrowsAny<CryptographicException>(() => cipher.DecryptBytes(encrypted, "message.body"));
        var other = new SensitiveDataCipher(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Assert.ThrowsAny<CryptographicException>(() => other.DecryptBytes(encrypted, "attachment.content"));
        encrypted[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => cipher.DecryptBytes(encrypted, "attachment.content"));
        encrypted[0] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => cipher.DecryptBytes(encrypted, "attachment.content"));
        Assert.ThrowsAny<CryptographicException>(() => cipher.DecryptText("unencrypted text", "message.body"));
    }

    [Fact]
    public void DatabaseConvertersEncryptSupportTextAndImagesAndSeparateModelsByKey()
    {
        var cipher = new SensitiveDataCipher(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var options = new DbContextOptionsBuilder<SentinelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var encrypted = new SentinelDbContext(options, dataCipher: cipher);
        using var plain = new SentinelDbContext(options);
        var property = encrypted.Model.FindEntityType(typeof(SelfServiceMessage))!.FindProperty(nameof(SelfServiceMessage.Body))!;
        var converter = property.GetValueConverter()!;
        var stored = (string)converter.ConvertToProvider("Nội dung riêng tư")!;
        Assert.DoesNotContain("Nội dung riêng tư", stored, StringComparison.Ordinal);
        Assert.Equal("Nội dung riêng tư", converter.ConvertFromProvider(stored));
        Assert.Null(plain.Model.FindEntityType(typeof(SelfServiceMessage))!.FindProperty(nameof(SelfServiceMessage.Body))!.GetValueConverter());
        var imageConverter = encrypted.Model.FindEntityType(typeof(SelfServiceAttachment))!.FindProperty(nameof(SelfServiceAttachment.Content))!.GetValueConverter()!;
        byte[] image = [137, 80, 78, 71, 13, 10, 26, 10];
        var storedImage = (byte[])imageConverter.ConvertToProvider(image)!;
        Assert.NotEqual(image, storedImage);
        Assert.Equal(image, imageConverter.ConvertFromProvider(storedImage));
    }
}
