using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class SelfServiceServiceCollectionExtensions
{
    public static IServiceCollection AddSelfService(this IServiceCollection services, IConfiguration configuration, string serverVaultKey)
    {
        services.AddScoped<ISelfServiceStore, SelfServiceStore>();
        services.AddScoped<ISelfServiceDirectory, SelfServiceDirectory>();
        services.AddScoped<ISelfServiceCommands, SelfServiceCommands>();
        services.AddScoped<ISelfServiceAudit, SelfServiceAudit>();
        services.AddScoped<SelfServiceService>();
        var attachmentQuota = new AttachmentQuotaSettings(
            configuration.GetValue("SENTINELLAN_ATTACHMENT_USER_DAILY_BYTES", 20L * 1024 * 1024),
            configuration.GetValue("SENTINELLAN_ATTACHMENT_ORGANIZATION_DAILY_BYTES", 200L * 1024 * 1024),
            configuration.GetValue("SENTINELLAN_ATTACHMENT_ORGANIZATION_TOTAL_BYTES", 2L * 1024 * 1024 * 1024));
        if (attachmentQuota.UserDailyBytes <= 0 || attachmentQuota.OrganizationDailyBytes < attachmentQuota.UserDailyBytes ||
            attachmentQuota.OrganizationTotalBytes < attachmentQuota.OrganizationDailyBytes || attachmentQuota.OrganizationTotalBytes > 1024L * 1024 * 1024 * 1024)
            throw new InvalidOperationException("Attachment quotas must be positive, ordered user daily <= organization daily <= total, and at most 1 TiB.");
        services.AddSingleton(attachmentQuota);
        services.AddSingleton<IMaintenanceCodeProtector>(new MaintenanceCodeProtector(serverVaultKey));
        var labIds = (configuration["SENTINELLAN_PANIC_LAB_DEVICE_IDS"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        services.AddSingleton(new SelfServiceSettings(configuration.GetValue<bool>("SENTINELLAN_PANIC_LAB_ENABLED"), labIds));
        services.AddHostedService<SelfServiceWorker>();
        services.AddHttpClient("expo-push", client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }
}
