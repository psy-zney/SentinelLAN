namespace SentinelLAN.Agent.Core;

public sealed record AgentPolicySnapshot(Guid Id, int IdleTimeoutMinutes, string UsbMode);
