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

public sealed class AgentApi(HttpClient httpClient, string deviceName) : IAgentApi
{
    public async Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/v1/agent/enroll", new { token, deviceName, osVersion = RuntimeInformation.OSDescription, agentVersion = "0.1.0" }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceIdentity>(cancellationToken))!;
    }

    public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken) =>
        SendHeartbeatAsync(identity, telemetry, null, cancellationToken);

    public async Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/agent/heartbeat", identity);
        request.Content = JsonContent.Create(new { idempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString("N"), telemetry.CpuPercent, telemetry.RamPercent, telemetry.DiskPercent, telemetry.OsVersion, telemetry.AgentVersion });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/agent/commands/poll", identity);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteCommand>(cancellationToken);
    }

    public async Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/agent/commands/{commandId}/result", identity);
        request.Content = JsonContent.Create(new { result.Succeeded, result.Message });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path, DeviceIdentity identity)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-SentinelLAN-Device-Id", identity.DeviceId.ToString());
        request.Headers.Add("X-SentinelLAN-Device-Secret", identity.DeviceSecret);
        return request;
    }
}
