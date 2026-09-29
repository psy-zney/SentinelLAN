using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public readonly record struct ActorContext(Guid UserId, Guid OrganizationId, string Role, string? SecurityStamp = null);
