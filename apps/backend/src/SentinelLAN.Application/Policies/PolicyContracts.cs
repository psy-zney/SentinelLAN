using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record PolicyDto(Guid Id, string Name, int IdleTimeoutMinutes, string UsbMode, int AssignedDeviceCount, DateTimeOffset CreatedAt);

public record AgentPolicyDto(Guid Id, int IdleTimeoutMinutes, string UsbMode);

public record CreatePolicyRequest(string Name, int IdleTimeoutMinutes, string UsbMode);

public record UpdatePolicyRequest(string Name, int IdleTimeoutMinutes, string UsbMode);

public record AssignPolicyRequest(Guid PolicyId, Guid DeviceId);
