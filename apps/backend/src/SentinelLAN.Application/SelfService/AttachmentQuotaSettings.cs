namespace SentinelLAN.Application;

public sealed record AttachmentQuotaSettings(long UserDailyBytes = 20 * 1024 * 1024,
    long OrganizationDailyBytes = 200 * 1024 * 1024, long OrganizationTotalBytes = 2L * 1024 * 1024 * 1024)
{
    public const int MinimumAccountingBytes = 4096;
}
