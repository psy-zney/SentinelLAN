using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public readonly record struct AgentContext(Guid DeviceId, Guid OrganizationId);
