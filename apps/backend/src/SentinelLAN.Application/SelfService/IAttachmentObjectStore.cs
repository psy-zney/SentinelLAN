namespace SentinelLAN.Application;

public interface IAttachmentObjectStore
{
    bool Enabled { get; }
    Task PutAsync(Guid organizationId, Guid requestId, Guid attachmentId, byte[] content, CancellationToken ct);
    Task<byte[]> GetAsync(Guid organizationId, Guid requestId, Guid attachmentId, int expectedSize, CancellationToken ct);
}
