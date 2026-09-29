using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record RevokeDeviceRequest(string Reason, bool Confirmed);
