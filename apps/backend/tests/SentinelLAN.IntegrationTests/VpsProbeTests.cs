using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class VpsProbeTests
{
    [Fact]
    public void ProbeJoinsFullIdsAndSeparatesStorageFromBlockIo()
    {
        const string id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var output = """
            HOST|Linux 6.8 x86_64
            UPTIME|up 2 days
            CPU|12.7
            MEM|64.2
            DISK|40
            SYSTEM|degraded
            SERVICE|docker|active|enabled
            DOCKER|available
            PS|{"ID":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","Names":"api","Image":"sentinellan:1","State":"running","Status":"Up 2 days (unhealthy)","Size":"10MB (virtual 200MB)"}
            PS|{"ID":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","Names":"worker","Image":"worker:1","State":"exited","Status":"Exited (1)","Size":"1MB (virtual 50MB)"}
            INSPECT|{"Id":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","RestartPolicy":"unless-stopped","Health":"unhealthy"}
            STATS|{"ID":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","CPUPerc":"240.50%","MemPerc":"25.10%","MemUsage":"100MiB / 400MiB","NetIO":"1MB / 2MB","BlockIO":"30MB / 40MB"}
            """;
        var result = VpsProbeParser.Parse(output);
        Assert.True(result.Success);
        Assert.Equal(12.7, result.CpuPercent);
        Assert.Equal(1, result.DockerContainersCount);
        var runtime = Assert.IsType<VpsRuntimeDto>(result.Runtime);
        Assert.Equal("degraded", runtime.SystemState);
        Assert.Equal("enabled", Assert.Single(runtime.Services).StartupState);
        Assert.Null(runtime.DockerError);
        var container = Assert.Single(runtime.Containers, row => row.Id == id);
        Assert.Equal(240.5, container.CpuPercent); // Docker sums usage across CPU cores.
        Assert.Equal("unhealthy", container.Health);
        Assert.Equal("unless-stopped", container.RestartPolicy);
        Assert.Equal("100MiB / 400MiB", container.MemoryUsage);
        Assert.Equal("10MB (virtual 200MB)", container.StorageUsage);
        Assert.Equal("30MB / 40MB", container.BlockIo);
        Assert.Null(runtime.Containers[1].CpuPercent);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unavailable")]
    public void UnavailableDockerRetainsHostHealthAndDoesNotReportZeroContainers(string state)
    {
        var result = VpsProbeParser.Parse($"HOST|Linux\nCPU|0\nSYSTEM|running\nDOCKER|{state}\n");
        Assert.True(result.Success);
        Assert.Equal(0, result.CpuPercent);
        Assert.Null(result.DockerContainersCount);
        Assert.False(result.Runtime!.DockerAvailable);
        Assert.NotNull(result.Runtime.DockerError);
    }

    [Fact]
    public void EmptyDockerIsDistinctFromFailedContainerListing()
    {
        var empty = VpsProbeParser.Parse("HOST|Linux\nDOCKER|available\n");
        var failed = VpsProbeParser.Parse("HOST|Linux\nDOCKER|available\nDOCKER_ERROR|Không đọc được danh sách container.\n");
        Assert.Equal(0, empty.DockerContainersCount);
        Assert.Null(empty.Runtime!.DockerError);
        Assert.Null(failed.DockerContainersCount);
        Assert.NotNull(failed.Runtime!.DockerError);
        Assert.False(VpsProbeParser.Parse("").Success);
    }

    [Theory]
    [InlineData("nginx; reboot")]
    [InlineData("$(shutdown -r now)")]
    [InlineData("--help")]
    [InlineData("NGINX")]
    public void RestartCatalogRejectsShellAndOptionInjection(string service) =>
        Assert.Throws<ArgumentException>(() => VpsCommandCatalog.BuildRestart(service));

    [Fact]
    public void CatalogSchedulesRebootAndVerifiesStartupChanges()
    {
        VpsOperationRequest Request(string action, string? target = null, string? value = null) => new(action, target, value, "Maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.Equal("(shutdown -r +1 || sudo -n shutdown -r +1)", VpsCommandCatalog.BuildOperation(Request("Reboot")));
        Assert.Contains("systemctl is-enabled --quiet docker", VpsCommandCatalog.BuildOperation(Request("EnableServiceStartup", "docker")));
        Assert.Contains("= disabled", VpsCommandCatalog.BuildOperation(Request("DisableServiceStartup", "nginx")));
        Assert.Contains("HostConfig.RestartPolicy.Name", VpsCommandCatalog.BuildOperation(Request("SetContainerRestartPolicy", new string('a', 64), "always")));
        Assert.Throws<ArgumentException>(() => VpsCommandCatalog.BuildOperation(Request("SetContainerRestartPolicy", "--help", "always")));
    }
}
