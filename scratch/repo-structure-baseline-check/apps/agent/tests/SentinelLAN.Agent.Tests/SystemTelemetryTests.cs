using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class SystemTelemetryTests
{
    [Fact]
    public void CpuUsesIntervalDeltasInsteadOfLifetimeProcessorTime()
    {
        var sampler = new CpuUsageSampler();
        Assert.Equal(0, sampler.Sample(8000, 10000));
        Assert.Equal(75, sampler.Sample(8025, 10100));
        Assert.Equal(0, sampler.Sample(8125, 10200));
        Assert.Equal(0, sampler.Sample(0, 0));
    }

    [Fact]
    public void LinuxParsersUseAvailableMemoryAndDoNotDoubleCountGuestCpu()
    {
        Assert.Equal((405UL, 465UL), SystemTelemetryCollector.ParseLinuxCpu("cpu  10 20 30 400 5 0 0 0 10 20"));
        Assert.Equal(75, SystemTelemetryCollector.ParseLinuxRam(["MemTotal: 1000 kB", "MemAvailable: 250 kB", "MemFree: 100 kB"]));
        Assert.Throws<IOException>(() => SystemTelemetryCollector.ParseLinuxRam(["MemFree: 100 kB"]));
    }

    [Fact]
    public async Task LocalSystemCollectorReturnsTechnicalMetricsInRange()
    {
        var collector = new SystemTelemetryCollector();
        await Task.Delay(100);
        var sample = collector.Collect();
        Assert.InRange(sample.CpuPercent, 0, 100);
        Assert.InRange(sample.RamPercent, 0.01, 100);
        Assert.InRange(sample.DiskPercent, 0, 100);
        Assert.False(string.IsNullOrWhiteSpace(sample.OsVersion));
    }
}
