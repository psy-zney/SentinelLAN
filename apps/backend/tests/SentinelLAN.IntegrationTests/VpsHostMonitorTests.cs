using System.Text.Json;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class VpsHostMonitorTests
{
    internal static VpsHostSnapshotDto Snapshot(DateTimeOffset time) => new("lab-vps", "192.0.2.1", time, "Linux", 1000,
        2, 25, 40, 1024, 256, 4096, 1638, new("running", true, null, [new("docker", "active", "enabled")],
            [new("abc", "api", "api:1", "running", "Up", null, "unless-stopped", 2, "1MiB / 4MiB", 25, "1MB", "1MB / 2MB", "0B / 0B",
                [new(8443, "tcp", "127.0.0.1", 3180), new(8080, "tcp", null, null)], [new("*", 8443, "tcp")], "bridge")]),
        [new("0.0.0.0", 9000, "tcp", "nginx")], []);

    private sealed class Reader(VpsHostSnapshotDto? snapshot, bool configured = true) : IVpsHostSnapshotReader
    {
        public bool Configured => configured;
        public int Reads { get; private set; }
        public Task<VpsHostSnapshotDto?> ReadAsync(CancellationToken ct) { Reads++; ct.ThrowIfCancellationRequested(); return Task.FromResult(snapshot); }
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static readonly ActorContext Owner = new(Guid.NewGuid(), Guid.NewGuid(), Roles.PlatformOwner);

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Technician)]
    [InlineData(Roles.Employee)]
    public async Task OtherRolesCannotReadHostSnapshot(string role)
    {
        var reader = new Reader(Snapshot(DateTimeOffset.UtcNow));
        var service = new VpsHostMonitorService(reader, TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStatusAsync(Owner with { Role = role }, CancellationToken.None));
        Assert.Equal(0, reader.Reads);
    }

    [Theory]
    [InlineData(-30, false)]
    [InlineData(-181, true)]
    [InlineData(61, true)]
    public async Task OldOrFutureSamplesAreNeverShownAsCurrent(int offsetSeconds, bool stale)
    {
        var now = DateTimeOffset.UtcNow;
        var service = new VpsHostMonitorService(new Reader(Snapshot(now.AddSeconds(offsetSeconds))), new Clock(now));
        var result = await service.GetStatusAsync(Owner, CancellationToken.None);
        Assert.True(result.Available);
        Assert.Equal(stale, result.Stale);
    }

    [Fact]
    public async Task UnconfiguredAndMissingSamplesStayDistinctAndCancellationPropagates()
    {
        var unconfigured = await new VpsHostMonitorService(new Reader(null, false), TimeProvider.System).GetStatusAsync(Owner, CancellationToken.None);
        Assert.False(unconfigured.Configured); Assert.False(unconfigured.Available);
        var service = new VpsHostMonitorService(new Reader(null), TimeProvider.System);
        var missing = await service.GetStatusAsync(Owner, CancellationToken.None);
        Assert.True(missing.Configured); Assert.False(missing.Available); Assert.Null(missing.Snapshot);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetStatusAsync(Owner, new CancellationToken(true)));
    }

    [Fact]
    public async Task ReaderRejectsMissingMalformedIncompleteAndOversizedFilesAndDropsUnknownFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"host-snapshot-{Guid.NewGuid():N}.json");
        var reader = new FileVpsHostSnapshotReader(path);
        try
        {
            Assert.Null(await reader.ReadAsync(CancellationToken.None));
            foreach (var content in new[] { "{invalid", "{}", new string('x', 2 * 1024 * 1024 + 1) })
            {
                await File.WriteAllTextAsync(path, content);
                Assert.Null(await reader.ReadAsync(CancellationToken.None));
            }
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(Snapshot(DateTimeOffset.UtcNow), options);
            await File.WriteAllTextAsync(path, json[..^1] + ",\"environment\":\"private-fixture-value\"}");
            var snapshot = await reader.ReadAsync(CancellationToken.None);
            Assert.NotNull(snapshot);
            Assert.DoesNotContain("private-fixture-value", JsonSerializer.Serialize(snapshot, options));
            Assert.Equal(8443, Assert.Single(snapshot.Runtime.Containers[0].ListeningPorts!).Port);
            Assert.Null(snapshot.Runtime.Containers[0].Ports![1].HostPort);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(new CancellationToken(true)));
        }
        finally { File.Delete(path); }
    }
}
