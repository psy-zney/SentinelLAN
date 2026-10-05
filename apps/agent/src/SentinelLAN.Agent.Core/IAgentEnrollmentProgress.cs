namespace SentinelLAN.Agent.Core;

public interface IAgentEnrollmentProgress
{
    Task ReportAsync(string state, CancellationToken cancellationToken);
}
