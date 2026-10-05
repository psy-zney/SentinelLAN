using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class FileAgentEnrollmentProgress(string path) : IAgentEnrollmentProgress
{
    public async Task ReportAsync(string state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new { state, recordedAt = DateTimeOffset.UtcNow }), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
