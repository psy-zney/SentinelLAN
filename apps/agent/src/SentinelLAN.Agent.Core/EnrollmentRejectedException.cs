namespace SentinelLAN.Agent.Core;

public sealed class EnrollmentRejectedException(string code) : Exception("Device enrollment was rejected.")
{
    public string Code { get; } = code;
}
