using System.Text.Json;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class FileVpsHostSnapshotReader(string? path) : IVpsHostSnapshotReader
{
    private const int MaximumBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public bool Configured => !string.IsNullOrWhiteSpace(path);

    public async Task<VpsHostSnapshotDto?> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Configured) return null;
        try
        {
            await using var file = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (file.Length is <= 0 or > MaximumBytes) return null;
            using var data = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int count;
            while ((count = await file.ReadAsync(buffer, ct)) != 0)
            {
                if (data.Length + count > MaximumBytes) return null;
                await data.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            var snapshot = JsonSerializer.Deserialize<VpsHostSnapshotDto>(data.GetBuffer().AsSpan(0, checked((int)data.Length)), JsonOptions);
            return snapshot is
            {
                Name.Length: > 0, Host.Length: > 0, Runtime.Services: not null, Runtime.Containers: not null,
                ListeningPorts: not null, Warnings: not null
            } && snapshot.CapturedAtUtc != default ? snapshot : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
