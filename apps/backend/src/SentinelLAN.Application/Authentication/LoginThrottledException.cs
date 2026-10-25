namespace SentinelLAN.Application;

public sealed class LoginThrottledException(TimeSpan retryAfter) : Exception("Bạn đã thử đăng nhập nhiều lần. Vui lòng chờ rồi thử lại.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
