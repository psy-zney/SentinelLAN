namespace SentinelLAN.Domain;

public sealed class OrganizationMonitoringSettings : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public int CpuThresholdPercent { get; set; } = 90;
    public int RamThresholdPercent { get; set; } = 90;
    public int DiskThresholdPercent { get; set; } = 95;
    public int OfflineMinutes { get; set; } = 5;
    public int SustainedMinutes { get; set; } = 5;
    public int MaintenancePauseMinutes { get; set; } = 15;
    public int PreventiveIntervalMonths { get; set; } = 6;
    public int WarrantyNoticeDays { get; set; } = 30;
    public bool SecurityAlertsEnabled { get; set; } = true;
    public Guid Revision { get; set; } = Guid.NewGuid();
}
