namespace SentinelLAN.Application;

public sealed record VpsStorageBreakdownDto(
    string Name,
    string Path,
    long SizeBytes,
    string? Category = null,
    string? Reclaimable = null);
