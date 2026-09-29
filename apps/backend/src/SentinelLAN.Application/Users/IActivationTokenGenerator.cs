using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IActivationTokenGenerator
{
    string GenerateToken();
}
