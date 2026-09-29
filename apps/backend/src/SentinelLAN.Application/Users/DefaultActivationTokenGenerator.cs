using SentinelLAN.Domain;

namespace SentinelLAN.Application;

internal sealed class DefaultActivationTokenGenerator : IActivationTokenGenerator
{
    public string GenerateToken() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
