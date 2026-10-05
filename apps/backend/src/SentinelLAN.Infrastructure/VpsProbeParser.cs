using System.Globalization;
using System.Text.Json;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public static class VpsProbeParser
{
    public static VpsMetricsResultDto Parse(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? Value(string tag) => lines.FirstOrDefault(line => line.StartsWith(tag + "|", StringComparison.Ordinal))?[(tag.Length + 1)..];
        var os = Value("HOST");
        if (string.IsNullOrWhiteSpace(os)) return new(false, "VPS không trả về dữ liệu probe hợp lệ.");
        var docker = Value("DOCKER");
        var services = lines.Where(line => line.StartsWith("SERVICE|", StringComparison.Ordinal)).Select(line => line.Split('|'))
            .Where(parts => parts.Length == 4).Select(parts => new VpsServiceStateDto(parts[1], EmptyState(parts[2]), EmptyState(parts[3]))).ToArray();
        var containers = JsonRows(lines, "PS");
        var inspected = JsonRows(lines, "INSPECT");
        var stats = JsonRows(lines, "STATS");
        var parsed = containers.Select(row =>
        {
            var id = Text(row, "ID") ?? "";
            var info = inspected.FirstOrDefault(item => Text(item, "Id") == id);
            var usage = stats.FirstOrDefault(item => Text(item, "ID") == id);
            var running = Text(row, "State") == "running";
            return new VpsContainerDto(id, Text(row, "Names") ?? id, Text(row, "Image") ?? "", Text(row, "State") ?? "unknown",
                Text(row, "Status") ?? "unknown", Text(info, "Health"), Text(info, "RestartPolicy"),
                running ? Percent(Text(usage, "CPUPerc"), false) : null, running ? Text(usage, "MemUsage") : null,
                running ? Percent(Text(usage, "MemPerc")) : null, Text(row, "Size"), running ? Text(usage, "NetIO") : null, running ? Text(usage, "BlockIO") : null,
                Ports(info), null, Text(info, "NetworkMode"));
        }).ToArray();
        var error = docker switch
        {
            "available" => string.Join(" ", lines.Where(line => line.StartsWith("DOCKER_ERROR|", StringComparison.Ordinal)).Select(line => line[13..])),
            "missing" => "VPS chưa cài Docker.",
            _ => "Không truy cập được Docker daemon. Kiểm tra dịch vụ và quyền tài khoản SSH."
        };
        var runtime = new VpsRuntimeDto(EmptyState(Value("SYSTEM") ?? ""), docker == "available", string.IsNullOrEmpty(error) ? null : error, services, parsed);
        return new(true, "Đã kiểm tra VPS.", os, Value("UPTIME"), Percent(Value("CPU")), Percent(Value("MEM")), Percent(Value("DISK")),
            docker == "available" && !lines.Any(line => line == "DOCKER_ERROR|Không đọc được danh sách container.") ? parsed.Count(c => c.State == "running") : null, runtime);
    }

    private static string EmptyState(string value) => string.IsNullOrEmpty(value) ? "unknown" : value;
    private static List<VpsPortBindingDto> Ports(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("Ports", out var ports) || ports.ValueKind != JsonValueKind.Object) return [];
        var result = new List<VpsPortBindingDto>();
        foreach (var entry in ports.EnumerateObject())
        {
            var key = entry.Name.Split('/');
            if (key.Length != 2 || !int.TryParse(key[0], out var port) || port is < 1 or > 65535 || key[1] is not ("tcp" or "udp" or "sctp")) continue;
            if (entry.Value.ValueKind == JsonValueKind.Array && entry.Value.GetArrayLength() > 0)
            {
                foreach (var binding in entry.Value.EnumerateArray())
                    if (int.TryParse(Text(binding, "HostPort"), out var hostPort) && hostPort is >= 1 and <= 65535)
                        result.Add(new(port, key[1], Text(binding, "HostIp"), hostPort));
            }
            else result.Add(new(port, key[1], null, null));
        }
        return result;
    }
    private static double? Percent(string? value, bool clamp = true) => double.TryParse(value?.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result)
        ? Math.Round(clamp ? Math.Clamp(result, 0, 100) : Math.Max(result, 0), 1) : null;
    private static string? Text(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static JsonElement[] JsonRows(string[] lines, string tag) => lines.Where(line => line.StartsWith(tag + "|", StringComparison.Ordinal)).Select(line =>
    {
        using var document = JsonDocument.Parse(line[(tag.Length + 1)..]);
        return document.RootElement.Clone();
    }).ToArray();
}
