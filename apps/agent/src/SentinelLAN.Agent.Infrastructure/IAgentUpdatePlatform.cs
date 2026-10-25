namespace SentinelLAN.Agent.Infrastructure;

public interface IAgentUpdatePlatform
{
    bool CanUpdate { get; }
    Version? InstalledVersion { get; }
    Task<bool> VerifyPackageAsync(string path, AgentRelease release, string publisherThumbprint, CancellationToken cancellationToken);
    Task<int> InstallAsync(string path);
    Task<bool> RestoreServiceAsync();
}
