using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record CreateUserOutcome(
    ManagementResultStatus Status,
    UserSummaryDto? User,
    string? ActivationToken = null,
    string? ActivationUrl = null,
    DateTimeOffset? ExpiresAt = null)
{
    public void Deconstruct(out ManagementResultStatus status, out UserSummaryDto? user)
    {
        status = Status;
        user = User;
    }
}
