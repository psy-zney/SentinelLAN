using System.Threading.RateLimiting;

namespace SentinelLAN.Api;

// Refill lazily using fractional credits, so burst capacity never truncates the configured rate.
internal sealed class IngressTokenBucket(int capacity, int perMinute, TimeProvider clock) : RateLimiter
{
    private readonly object gate = new();
    private decimal credits = (decimal)capacity * TimeSpan.TicksPerMinute;
    private long updatedAt = clock.GetTimestamp();
    private long lastAttempt = clock.GetTimestamp();
    private long successful;
    private long failed;
    private bool disposed;

    private void Refill(long now)
    {
        credits = Math.Min((decimal)capacity * TimeSpan.TicksPerMinute,
            credits + (decimal)clock.GetElapsedTime(updatedAt, now).Ticks * perMinute);
        updatedAt = now;
    }

    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (gate)
            {
                var now = clock.GetTimestamp();
                Refill(now);
                return credits == (decimal)capacity * TimeSpan.TicksPerMinute ? clock.GetElapsedTime(lastAttempt, now) : null;
            }
        }
    }

    public override RateLimiterStatistics GetStatistics()
    {
        lock (gate)
        {
            Refill(clock.GetTimestamp());
            return new RateLimiterStatistics
            {
                CurrentAvailablePermits = (long)(credits / TimeSpan.TicksPerMinute),
                CurrentQueuedCount = 0,
                TotalSuccessfulLeases = successful,
                TotalFailedLeases = failed
            };
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            lastAttempt = clock.GetTimestamp();
            Refill(lastAttempt);
            var required = (decimal)permitCount * TimeSpan.TicksPerMinute;
            if (credits >= required)
            {
                credits -= required;
                successful++;
                return new Lease(true, null);
            }
            failed++;
            var ticks = decimal.Ceiling((required - credits) / perMinute);
            return new Lease(false, TimeSpan.FromTicks((long)ticks));
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AttemptAcquireCore(permitCount));
    }

    protected override void Dispose(bool disposing)
    {
        lock (gate) disposed = true;
        base.Dispose(disposing);
    }

    private sealed class Lease(bool acquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => acquired;
        public override IEnumerable<string> MetadataNames => retryAfter is null ? [] : [MetadataName.RetryAfter.Name];
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
            return metadata is not null;
        }
    }
}
