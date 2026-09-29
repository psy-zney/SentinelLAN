using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IEnrollmentSecretGenerator
{
    string GenerateToken();
}
