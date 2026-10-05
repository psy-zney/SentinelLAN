namespace SentinelLAN.Application;

public sealed record VpsHostStatusDto(bool Configured, bool Available, bool Stale, string Message, VpsHostSnapshotDto? Snapshot);
