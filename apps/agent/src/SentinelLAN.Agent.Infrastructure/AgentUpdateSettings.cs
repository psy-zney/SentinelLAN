namespace SentinelLAN.Agent.Infrastructure;

public sealed record AgentUpdateSettings(bool Enabled, string ManifestUrl, string PublicKeyPem,
    string PublisherThumbprint, string CompanyServerUrl);
