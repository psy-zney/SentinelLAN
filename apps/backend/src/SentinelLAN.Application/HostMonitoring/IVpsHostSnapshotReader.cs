namespace SentinelLAN.Application;

public interface IVpsHostSnapshotReader
{
    bool Configured { get; }
    Task<VpsHostSnapshotDto?> ReadAsync(CancellationToken ct);
}
