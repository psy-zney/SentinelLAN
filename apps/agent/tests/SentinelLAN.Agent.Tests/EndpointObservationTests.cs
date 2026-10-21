using System.Net;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class EndpointObservationTests
{
    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("fc00::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("2001:db8::1", false)]
    public void InventoryCollectsOnlyLanAddresses(string address, bool expected) =>
        Assert.Equal(expected, EndpointPlatformProbe.IsLanAddress(IPAddress.Parse(address)));

    [Fact]
    public async Task ProbeIsCachedAndUnknownProtectionIsPreserved()
    {
        var clock = new Clock(DateTimeOffset.UtcNow);
        var probe = new Probe(clock);
        using var collector = new EndpointObservationCollector(probe, clock);
        Assert.NotNull(await collector.GetInventoryAsync(default));
        var first = await collector.CollectAsync(default);
        Assert.NotNull(first.Inventory);
        Assert.Equal("Unknown", first.Security!.AntivirusStatus);
        Assert.Null((await collector.CollectAsync(default)).Inventory);
        Assert.Equal(1, probe.Reads);
        clock.Now = clock.Now.AddMinutes(2);
        Assert.NotNull((await collector.CollectAsync(default)).Inventory);
        Assert.Equal(2, probe.Reads);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Probe(Clock clock) : IEndpointPlatformProbe
    {
        public int Reads { get; private set; }
        public Task<EndpointObservations> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult(new EndpointObservations(new(clock.Now, "LAB-001", "Lab", "Fixture", "CPU", 8L * 1024 * 1024 * 1024, 128L * 1024 * 1024 * 1024, []),
                new(clock.Now, "Unknown", "Unknown", "Unknown", "Unknown", "NotSupported")));
        }
    }
}
