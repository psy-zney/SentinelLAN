namespace SentinelLAN.Agent.Core;

public interface IEnrollmentAttemptStore
{
    Task<string> GetOrCreateSecretAsync(string token, string server, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
