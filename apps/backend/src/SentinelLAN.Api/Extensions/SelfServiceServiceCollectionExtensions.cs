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
