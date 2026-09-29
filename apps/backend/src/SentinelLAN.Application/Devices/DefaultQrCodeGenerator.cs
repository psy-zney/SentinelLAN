using SentinelLAN.Domain;

namespace SentinelLAN.Application;

internal sealed class DefaultQrCodeGenerator : IQrCodeGenerator
{
    public string GenerateOpaqueCode() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
