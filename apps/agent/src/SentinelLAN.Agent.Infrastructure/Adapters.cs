using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class DevelopmentIdentityStore(string path) : IDeviceIdentityStore
{
    public async Task<DeviceIdentity?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DeviceIdentity>(stream, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, identity, cancellationToken: cancellationToken);
    }
}

public sealed class SystemTelemetryCollector : ITelemetryCollector
{
    public TelemetrySnapshot Collect()
    {
        var memory = GC.GetGCMemoryInfo();
        var ram = memory.TotalAvailableMemoryBytes <= 0 ? 0 : Math.Clamp(GC.GetTotalMemory(false) * 100d / memory.TotalAvailableMemoryBytes, 0, 100);
        var drive = DriveInfo.GetDrives().FirstOrDefault(x => x.IsReady);
        var disk = drive is null || drive.TotalSize == 0 ? 0 : (drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize;
        return new TelemetrySnapshot(Math.Clamp(Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds / Math.Max(Environment.ProcessorCount * 1000d, 1), 0, 100), ram, disk, RuntimeInformation.OSDescription, "0.1.0");
    }
}

public sealed class AgentApi(HttpClient httpClient, string deviceName) : IAgentApi
{
    public async Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/api/v1/agent/enroll", new { token, deviceName, osVersion = RuntimeInformation.OSDescription, agentVersion = "0.1.0" }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceIdentity>(cancellationToken))!;
    }

    public async Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/api/v1/agent/heartbeat", new { identity.DeviceId, identity.DeviceSecret, idempotencyKey = Guid.NewGuid().ToString("N"), telemetry.CpuPercent, telemetry.RamPercent, telemetry.DiskPercent, telemetry.OsVersion, telemetry.AgentVersion }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync($"/api/v1/agent/commands/poll?deviceId={identity.DeviceId}&deviceSecret={Uri.EscapeDataString(identity.DeviceSecret)}", null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteCommand>(cancellationToken);
    }

    public async Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/v1/agent/commands/{commandId}/result", new { identity.DeviceId, identity.DeviceSecret, result.Succeeded, result.Message }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
