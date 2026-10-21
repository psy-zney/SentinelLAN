using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AttachmentObjectStoreTests
{
    [Fact]
    public async Task ExternalImagesAreEncryptedBoundToTenantRequestAndAttachmentAndSizeLimited()
    {
        using var transport = new MemoryS3(); var cipher = new SensitiveDataCipher(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        using var objects = new AttachmentObjectStore(transport, "fixture-bucket", cipher);
        var org = Guid.NewGuid(); var request = Guid.NewGuid(); var attachment = Guid.NewGuid(); var image = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        await objects.PutAsync(org, request, attachment, image, default);
        Assert.Equal(image.Length + 33, transport.Data.Length); Assert.False(image.AsSpan().SequenceEqual(transport.Data));
        Assert.Equal(image, await objects.GetAsync(org, request, attachment, image.Length, default));
        await Assert.ThrowsAnyAsync<CryptographicException>(() => objects.GetAsync(Guid.NewGuid(), request, attachment, image.Length, default));
        await Assert.ThrowsAnyAsync<CryptographicException>(() => objects.GetAsync(org, Guid.NewGuid(), attachment, image.Length, default));
        await Assert.ThrowsAsync<IOException>(() => objects.GetAsync(org, request, attachment, image.Length + 1, default));
        transport.Data[^1] ^= 1;
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => objects.GetAsync(org, request, attachment, image.Length, default));
    }
    private sealed class MemoryS3() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://s3.example.test", ForcePathStyle = true })
    {
        public byte[] Data { get; private set; } = [];
        public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream(); await request.InputStream.CopyToAsync(copy, cancellationToken); Data = copy.ToArray(); return new();
        }
        public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new GetObjectResponse { ContentLength = Data.Length, ResponseStream = new MemoryStream(Data, writable: false) });
    }
}
