using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class LoginAttemptLimiter(SentinelDbContext db, TimeProvider clock) : ILoginAttemptLimiter
{
    private static readonly SemaphoreSlim DevelopmentGate = new(1, 1);

    public Task<TimeSpan?> ReserveAsync(string organizationCode, string email, CancellationToken cancellationToken) =>
        MutateAsync(organizationCode, email, reset: false, cancellationToken);

    public async Task ResetAsync(string organizationCode, string email, CancellationToken cancellationToken) =>
        _ = await MutateAsync(organizationCode, email, reset: true, cancellationToken);

    private async Task<TimeSpan?> MutateAsync(string organizationCode, string email, bool reset, CancellationToken ct)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{organizationCode.Trim().ToLowerInvariant()}\n{email.Trim().ToLowerInvariant()}"));
        var partition = Convert.ToHexString(digest);
        var relational = db.Database.IsRelational();
        if (!relational) await DevelopmentGate.WaitAsync(ct);
        await using var transaction = relational ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (relational)
            {
                var lockId = BinaryPrimitives.ReadInt64BigEndian(digest);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
            }
            var bucket = await db.Set<LoginAttemptBucket>().SingleOrDefaultAsync(x => x.PartitionHash == partition, ct);
            if (reset)
            {
                if (bucket is not null) db.Remove(bucket);
            }
            else
            {
                if (bucket is null)
                {
                    bucket = new LoginAttemptBucket { PartitionHash = partition };
                    db.Add(bucket);
                }
                var retryAfter = bucket.Reserve(clock.GetUtcNow());
                if (retryAfter is not null) return retryAfter; // Rejected attempts never extend the delay.
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return null;
        }
        finally { if (!relational) DevelopmentGate.Release(); }
    }
}
