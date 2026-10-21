namespace SentinelLAN.Api;

// EF InMemory has no transaction rollback. Serialize its multi-entity writes;
// PostgreSQL uses transactions, unique constraints and optimistic concurrency.
internal sealed class DevelopmentWriteGate : IDisposable
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private DevelopmentWriteGate() { }

    public static async Task<DevelopmentWriteGate> EnterAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        return new DevelopmentWriteGate();
    }

    public void Dispose() => Gate.Release();
}
