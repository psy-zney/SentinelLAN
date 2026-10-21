using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AttachmentQueryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListAndCountQueriesNeverSelectImageContentAndKeepTenantAndRequestFilters(bool count)
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<SentinelDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=dummy;Password=dummy")
            .AddInterceptors(new SuppressConnection(), new CaptureQuery())
            .Options;
        var cipher = new SensitiveDataCipher(Convert.ToBase64String(new byte[32]));
        await using var db = new SentinelDbContext(options, dataCipher: cipher);
        var store = new SelfServiceStore(db);
        // The actual Npgsql translation runs; interception prevents all database I/O.
        var query = await Assert.ThrowsAsync<CapturedQueryException>(async () =>
        {
            if (count) await store.CountAttachmentsAsync(organizationId, requestId, default);
            else await store.GetAttachmentsAsync(organizationId, requestId, default);
        });
        Assert.DoesNotContain("\"Content\"", query.Sql, StringComparison.Ordinal);
        Assert.Contains("\"OrganizationId\" =", query.Sql, StringComparison.Ordinal);
        Assert.Contains("\"RequestId\" =", query.Sql, StringComparison.Ordinal);
        Assert.Contains(organizationId, query.Parameters);
        Assert.Contains(requestId, query.Parameters);
        if (count) Assert.Contains("count(*)", query.Sql, StringComparison.Ordinal);
        else
        {
            Assert.Contains("\"Size\"", query.Sql, StringComparison.Ordinal);
            Assert.Contains("ORDER BY", query.Sql, StringComparison.Ordinal);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private sealed class CapturedQueryException(string sql, object[] parameters) : Exception("Query captured without execution")
    {
        public string Sql { get; } = sql;
        public object[] Parameters { get; } = parameters;
    }

    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class CaptureQuery : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            throw new CapturedQueryException(command.CommandText, command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value!).ToArray());
    }
}
