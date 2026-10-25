using System.Globalization;
using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class LoginThrottleFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (LoginThrottledException error)
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(error.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return Results.Problem(error.Message, statusCode: StatusCodes.Status429TooManyRequests);
        }
    }
}
