namespace SentinelLAN.Agent.Infrastructure;

public sealed record AgentExecutionOptions(
    bool LabExecution = false,
    Guid AuthorizedDeviceId = default,
    bool AllowServiceRestart = false,
    bool AllowPolicyChanges = false,
    string? IsolationServerIpv4 = null,
    bool AllowApprovedAppInstall = false,
    bool AllowAgentMaintenance = false,
    string[]? TrustedPackageHosts = null,
    long MaxPackageBytes = 100 * 1024 * 1024);
