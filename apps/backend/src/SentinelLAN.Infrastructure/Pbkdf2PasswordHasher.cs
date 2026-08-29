using System.Globalization;
using System.Security.Cryptography;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const string Prefix = "pbkdf2-sha256";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashLength);
        return string.Join('$', Prefix, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash)) return PasswordVerificationResult.Failed;
        var parts = passwordHash.Split('$');
        if (parts.Length == 4 && parts[0] == Prefix && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) && iterations is >= 10_000 and <= 1_000_000)
        {
            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length < SaltLength || expected.Length != HashLength) return PasswordVerificationResult.Failed;
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordVerificationResult.Failed;
                return iterations < Iterations ? PasswordVerificationResult.SuccessNeedsRehash : PasswordVerificationResult.Success;
            }
            catch (FormatException)
            {
                return PasswordVerificationResult.Failed;
            }
        }

        return IsLegacySha256(password, passwordHash)
            ? PasswordVerificationResult.SuccessNeedsRehash
            : PasswordVerificationResult.Failed;
    }

    private static bool IsLegacySha256(string password, string passwordHash)
    {
        if (passwordHash.Length != 64 || !passwordHash.All(Uri.IsHexDigit)) return false;
        var actual = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(passwordHash));
    }
}
