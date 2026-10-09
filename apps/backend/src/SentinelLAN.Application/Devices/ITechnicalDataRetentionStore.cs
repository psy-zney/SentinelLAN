namespace SentinelLAN.Application;

public interface ITechnicalDataRetentionStore
{
    Task<int> DeleteBatchAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken);
}
