using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface ISecretHasher
{
    string Create(string value);
}
