using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class PaginationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (ArgumentOutOfRangeException error) when (error.ParamName == nameof(PageWindow))
        {
            return Results.Problem("Trang dữ liệu không hợp lệ. Vui lòng tải lại danh sách.", statusCode: 400);
        }
    }
}
