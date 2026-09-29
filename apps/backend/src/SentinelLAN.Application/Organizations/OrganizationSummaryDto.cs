using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record OrganizationSummaryDto(Guid Id, string Code, string Name, DateTimeOffset CreatedAt);
