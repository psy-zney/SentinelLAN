using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record MonitoringSettingsDto(int CpuThresholdPercent = 90, int RamThresholdPercent = 90,
    int DiskThresholdPercent = 95, int OfflineMinutes = 5, int SustainedMinutes = 5,
    int MaintenancePauseMinutes = 15, int PreventiveIntervalMonths = 6, int WarrantyNoticeDays = 30,
    bool SecurityAlertsEnabled = true)
{
    public Guid Revision { get; init; }
}

public sealed record UpdateMonitoringSettingsRequest(MonitoringSettingsDto Settings, string Reason,
    bool Confirmed, Guid Nonce, DateTimeOffset ExpiresAt);

public interface IMonitoringSettingsStore
{
    Task<OrganizationMonitoringSettings?> FindAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> NonceExistsAsync(Guid organizationId, Guid nonce, CancellationToken cancellationToken);
    void Add(OrganizationMonitoringSettings settings);
    void Reserve(AdministrativeRequestNonce nonce);
    void Audit(AuditLog audit);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}

public interface IMonitoringChangeSignal
{
    void RequestCheck(Guid organizationId);
}

public sealed class OrganizationConfigurationException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
