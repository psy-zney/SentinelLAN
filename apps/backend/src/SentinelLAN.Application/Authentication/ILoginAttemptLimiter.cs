namespace SentinelLAN.Application;

public interface ILoginAttemptLimiter
{
    Task<TimeSpan?> ReserveAsync(string organizationCode, string email, CancellationToken cancellationToken);
    Task ResetAsync(string organizationCode, string email, CancellationToken cancellationToken);
}
