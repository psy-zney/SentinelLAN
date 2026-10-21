using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed record AttachmentObjectStoreOptions(bool Enabled, string? Endpoint, string? Region, string? Bucket, string? AccessKey, string? SecretKey, bool ForcePathStyle = false);

public sealed class AttachmentObjectStore : IAttachmentObjectStore, IDisposable
{
    private readonly IAmazonS3? client;
    private readonly string? bucket;
    private readonly SensitiveDataCipher cipher;
    public bool Enabled => client is not null;
    public AttachmentObjectStore(IAmazonS3 client, string bucket, SensitiveDataCipher cipher) { this.client = client; this.bucket = bucket; this.cipher = cipher; }
    public AttachmentObjectStore(AttachmentObjectStoreOptions options, SensitiveDataCipher cipher)
    {
        this.cipher = cipher;
        if (!options.Enabled) return;
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 || endpoint.AbsolutePath != "/" ||
            string.IsNullOrWhiteSpace(options.Bucket) || options.Bucket.Length is < 3 or > 63 || options.Bucket.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '.') ||
            string.IsNullOrWhiteSpace(options.Region) || options.Region.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
            string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
            throw new InvalidOperationException("S3 attachments require a trusted HTTPS endpoint, bucket, region and scoped access credentials.");
        bucket = options.Bucket;
        client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), new AmazonS3Config
        {
            ServiceURL = endpoint.GetLeftPart(UriPartial.Authority),
            AuthenticationRegion = options.Region,
            ForcePathStyle = options.ForcePathStyle,
            Timeout = TimeSpan.FromSeconds(15),
            MaxErrorRetry = 2
        });
    }
    public static string Key(Guid org, Guid request, Guid attachment) => $"attachments/{org:N}/{request:N}/{attachment:N}";
    private static string Purpose(Guid org, Guid request, Guid attachment) => $"attachment.object:{org:D}:{request:D}:{attachment:D}";
    public async Task PutAsync(Guid org, Guid request, Guid attachment, byte[] content, CancellationToken ct)
    {
        if (client is null || content.Length is < 1 or > 2 * 1024 * 1024) throw new InvalidOperationException("Attachment object storage is unavailable.");
        using var stream = new MemoryStream(cipher.EncryptBytes(content, Purpose(org, request, attachment)), writable: false);
        await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = Key(org, request, attachment), InputStream = stream, ContentType = "application/octet-stream", AutoCloseStream = false }, ct);
    }
    public async Task<byte[]> GetAsync(Guid org, Guid request, Guid attachment, int expectedSize, CancellationToken ct)
    {
        if (client is null || expectedSize is < 1 or > 2 * 1024 * 1024) throw new IOException("External attachment storage is not configured.");
        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = Key(org, request, attachment) }, ct);
            var length = checked(expectedSize + 33);
            if (response.ContentLength != length) throw new IOException("Attachment envelope has an unexpected length.");
            var bytes = new byte[length]; await response.ResponseStream.ReadExactlyAsync(bytes, ct);
            if (response.ResponseStream.ReadByte() != -1) throw new IOException("Attachment exceeds its size limit.");
            var plain = cipher.DecryptBytes(bytes, Purpose(org, request, attachment));
            if (plain.Length != expectedSize) throw new IOException("Attachment size does not match metadata.");
            return plain;
        }
        catch (AmazonS3Exception error) { throw new IOException("External attachment storage is temporarily unavailable.", error); }
        catch (HttpRequestException error) { throw new IOException("External attachment storage could not be reached.", error); }
        catch (OperationCanceledException error) when (!ct.IsCancellationRequested) { throw new IOException("External attachment storage timed out.", error); }
    }
    public void Dispose() => client?.Dispose();
}
