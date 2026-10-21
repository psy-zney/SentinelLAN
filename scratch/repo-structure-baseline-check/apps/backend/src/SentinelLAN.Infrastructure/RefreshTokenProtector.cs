using System.Security.Cryptography;
using System.Text;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class RefreshTokenProtector : IRefreshTokenProtector
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
