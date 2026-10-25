using System.Globalization;
using System.Threading.RateLimiting;

namespace SentinelLAN.Api;

public sealed class IngressRateLimitMiddleware(RequestDelegate next, IngressRequestBudget budget)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/hubs"))
        {
            await next(context);
            return;
        }
        using var lease = await budget.Limiter.AcquireAsync(context, cancellationToken: context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.Headers.RetryAfter = (lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                ? Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds)) : 1).ToString(CultureInfo.InvariantCulture);
            await Results.Problem("Máy chủ đang nhận nhiều yêu cầu. Vui lòng chờ rồi thử lại.", statusCode: 429).ExecuteAsync(context);
            return;
        }
        await next(context);
    }
}

public sealed class IngressRequestBudget : IDisposable
{
    public PartitionedRateLimiter<HttpContext> Limiter { get; }

    public IngressRequestBudget(IConfiguration configuration, TimeProvider? clock = null)
    {
        var perMinute = configuration.GetValue("SENTINELLAN_INGRESS_REQUESTS_PER_MINUTE", 600);
        var burst = configuration.GetValue("SENTINELLAN_INGRESS_BURST", 300);
        var concurrent = configuration.GetValue("SENTINELLAN_INGRESS_CONCURRENCY", 128);
        if (perMinute is < 30 or > 60000 || burst is < 1 or > 10000 || concurrent is < 1 or > 4096)
            throw new InvalidOperationException("Ingress request, burst and concurrency budgets are outside their supported bounds.");
        Limiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetConcurrencyLimiter(
                context.Request.Path.StartsWithSegments("/hubs") ? "realtime" : "api",
                partition => new ConcurrencyLimiterOptions { PermitLimit = partition == "realtime" ? 512 : concurrent, QueueLimit = 0 })),
            PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.Get(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new IngressTokenBucket(burst, perMinute, clock ?? TimeProvider.System))));
    }

    public void Dispose() => Limiter.Dispose();
}
