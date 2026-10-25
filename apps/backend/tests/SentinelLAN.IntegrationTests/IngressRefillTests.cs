using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SentinelLAN.Api;

namespace SentinelLAN.IntegrationTests;

public sealed class IngressRefillTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(61)]
    [InlineData(600)]
    [InlineData(60000)]
    public void SustainedRatePreservesFractionalCreditsIndependentlyOfBurst(int perMinute)
    {
        var clock = new ManualClock();
        using var budget = new IngressRequestBudget(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SENTINELLAN_INGRESS_REQUESTS_PER_MINUTE"] = perMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SENTINELLAN_INGRESS_BURST"] = "3"
        }).Build(), clock);
        var context = new DefaultHttpContext();
        for (var i = 0; i < 3; i++)
        {
            using var lease = budget.Limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }
        using (var empty = budget.Limiter.AttemptAcquire(context)) Assert.False(empty.IsAcquired);

        for (var i = 1; i <= perMinute; i++)
        {
            var nextToken = ((long)i * TimeSpan.TicksPerMinute + perMinute - 1) / perMinute;
            clock.Timestamp = nextToken - 1;
            using (var early = budget.Limiter.AttemptAcquire(context)) Assert.False(early.IsAcquired);
            clock.Timestamp = nextToken;
            using var available = budget.Limiter.AttemptAcquire(context);
            Assert.True(available.IsAcquired);
        }
        Assert.Equal(TimeSpan.TicksPerMinute, clock.Timestamp);
        using (var empty = budget.Limiter.AttemptAcquire(context)) Assert.False(empty.IsAcquired);

        clock.Timestamp += TimeSpan.TicksPerHour;
        for (var i = 0; i < 3; i++)
        {
            using var lease = budget.Limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }
        using var capped = budget.Limiter.AttemptAcquire(context);
        Assert.False(capped.IsAcquired);
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Timestamp;
    }
}
