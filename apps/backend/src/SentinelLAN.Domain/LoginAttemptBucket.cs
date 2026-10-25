namespace SentinelLAN.Domain;

// The partition is a hash of tenant code + account, including accounts that do not exist.
public sealed class LoginAttemptBucket
{
    public required string PartitionHash { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAllowedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public TimeSpan? Reserve(DateTimeOffset now)
    {
        if (ExpiresAt <= now) Attempts = 0;
        else if (NextAllowedAt > now) return NextAllowedAt - now;
        Attempts = Math.Min(Attempts + 1, 12);
        NextAllowedAt = now.AddSeconds(Attempts < 5 ? 0 : Math.Min(60, 1 << (Attempts - 5)));
        ExpiresAt = now.AddMinutes(15);
        return null;
    }
}
