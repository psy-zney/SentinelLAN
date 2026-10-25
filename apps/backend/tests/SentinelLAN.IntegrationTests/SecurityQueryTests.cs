using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class SecurityQueryTests
{
    [Theory]
    [InlineData("users")]
    [InlineData("requests")]
    [InlineData("incidents")]
    [InlineData("work-orders")]
    [InlineData("health")]
    [InlineData("quota")]
    [InlineData("commands")]
    public async Task ProductionQueriesKeepBoundsAndScopeWithoutDatabaseIo(string kind)
    {
        var org = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = new SentinelDbContext(new DbContextOptionsBuilder<SentinelDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=dummy;Password=dummy")
            .AddInterceptors(new NoConnection(), new CaptureCommand()).Options);
        var query = await Assert.ThrowsAsync<CapturedSql>(async () =>
        {
            switch (kind)
            {
                case "users": await new ManagementStore(db).GetUserPageAsync(org, new(3, 5), DateTimeOffset.UtcNow, default); break;
                case "requests": await new SelfServiceStore(db).GetRequestPageAsync(org, user, Guid.NewGuid(), new(3, 5), true, default); break;
                case "incidents": await new AssetStore(db).GetIncidentPageAsync(org, null, user, new(3, 5), default); break;
                case "work-orders": await new AssetStore(db).GetWorkOrderPageAsync(org, null, user, new(3, 5), default); break;
                case "health": await new DeviceHealthReader(db).GetDevicesAsync(org, DateTimeOffset.UtcNow, default); break;
                case "quota": await new SelfServiceStore(db).GetAttachmentUsageAsync(org, user, new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero), default); break;
                case "commands": await new SelfServiceCommands(db, new HmacCommandSigner("dummy-local-key")).GetManyAsync(org, [Guid.NewGuid(), Guid.NewGuid()], default); break;
            }
        });
        Assert.Contains(org, query.Parameters);
        Assert.Contains("\"OrganizationId\"", query.Sql, StringComparison.Ordinal);
        if (kind is "users" or "requests" or "incidents" or "work-orders")
        {
            Assert.Contains("LIMIT", query.Sql, StringComparison.Ordinal);
            Assert.Contains("OFFSET", query.Sql, StringComparison.Ordinal);
        }
        if (kind is "incidents" or "work-orders") Assert.Contains("\"AssignedUserId\"", query.Sql, StringComparison.Ordinal);
        if (kind == "users")
        {
            Assert.Contains("EXISTS", query.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("\"PasswordHash\"", query.Sql, StringComparison.Ordinal);
        }
        if (kind == "quota")
        {
            Assert.DoesNotContain("\"Content\"", query.Sql, StringComparison.Ordinal);
            Assert.Contains("sum(", query.Sql, StringComparison.OrdinalIgnoreCase);
        }
        if (kind == "health")
        {
            Assert.True(query.Sql.Contains("LIMIT", StringComparison.Ordinal) || query.Sql.Contains("ROW_NUMBER", StringComparison.Ordinal), query.Sql);
            Assert.True(query.Sql.Contains("1024", StringComparison.Ordinal) || query.Parameters.Contains(1024), query.Sql);
        }
    }

    private sealed class CapturedSql(string sql, object[] parameters) : Exception("SQL intercepted before execution")
    {
        public string Sql { get; } = sql;
        public object[] Parameters { get; } = parameters;
    }
    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
    }
    private sealed class CaptureCommand : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            throw new CapturedSql(command.CommandText, command.Parameters.Cast<DbParameter>().Select(x => x.Value!).ToArray());
    }
}
