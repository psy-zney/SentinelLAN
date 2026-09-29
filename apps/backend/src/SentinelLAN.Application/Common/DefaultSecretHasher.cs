using SentinelLAN.Domain;

namespace SentinelLAN.Application;

internal sealed class DefaultSecretHasher : ISecretHasher
{
    public string Create(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
