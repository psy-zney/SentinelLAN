namespace SentinelLAN.Application;

public sealed class TechnicalDataRetentionService(ITechnicalDataRetentionStore store, TimeProvider clock)
{
    public const int RetentionDays = 30;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-RetentionDays);
        for (var batch = 0; batch < 10; batch++)
            if (await store.DeleteBatchAsync(cutoff, 1000, cancellationToken) == 0) break;
    }
}
