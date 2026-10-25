namespace SentinelLAN.Agent.Infrastructure;

public sealed record AgentRelease(int SchemaVersion, Guid ReleaseId, string Version, string PackageUrl,
    string Sha256, long SizeBytes, DateTimeOffset PublishedAt, DateTimeOffset ExpiresAt,
    string CompanyServerUrl, string Reason);
