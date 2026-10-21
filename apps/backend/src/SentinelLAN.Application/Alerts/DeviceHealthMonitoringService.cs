using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class DeviceHealthMonitoringService(IDeviceHealthReader devices, IAutomaticAlertStore alerts, TimeProvider clock,
    IMonitoringSettingsStore? settingsStore = null)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var organizationId in await devices.GetActiveOrganizationsAsync(cancellationToken))
            await CheckOrganizationAsync(organizationId, cancellationToken);
    }

    public async Task RunOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        if ((await devices.GetActiveOrganizationsAsync(ct)).Contains(organizationId))
            await CheckOrganizationAsync(organizationId, ct);
    }

    private async Task CheckOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var settings = MonitoringSettingsService.ToDto(settingsStore is null ? null : await settingsStore.FindAsync(organizationId, cancellationToken));
        {
            foreach (var device in await devices.GetDevicesAsync(organizationId, now, cancellationToken))
            {
                var existing = await alerts.GetRecentAsync(organizationId, device.DeviceId, now.AddHours(-1), cancellationToken);
                var latest = device.Measurements.OrderByDescending(m => m.CollectedAt).FirstOrDefault();
                var fresh = latest is not null && latest.CollectedAt >= now.AddMinutes(-2) && latest.CollectedAt <= now;
                var protectionFresh = settings.SecurityAlertsEnabled && fresh && latest!.ProtectionCollectedAt >= now.AddMinutes(-2) && latest.ProtectionCollectedAt <= now;
                // Null means the condition cannot currently be evaluated; do not call missing telemetry a recovery.
                var rules = new (string Key, bool? Breached, string Message)[]
                {
                    ("Offline", device.LastSeenAt is null ? null : device.MaintenanceUntil > now ? false : device.LastSeenAt <= now.AddMinutes(-settings.OfflineMinutes), $"Máy {device.Name} đã mất kết nối ít nhất {settings.OfflineMinutes} phút. Kiểm tra nguồn điện và mạng."),
                    ("DiskHigh", fresh ? latest!.DiskPercent >= settings.DiskThresholdPercent : null, $"Ổ đĩa hệ thống của máy {device.Name} đã sử dụng ít nhất {settings.DiskThresholdPercent}%. IT cần kiểm tra dung lượng."),
                    ("CpuHigh", fresh ? Sustained(device.Measurements, now, settings.SustainedMinutes, m => m.CpuPercent >= settings.CpuThresholdPercent) : null, $"CPU của máy {device.Name} ở mức ít nhất {settings.CpuThresholdPercent}% trong {settings.SustainedMinutes} phút."),
                    ("RamHigh", fresh ? Sustained(device.Measurements, now, settings.SustainedMinutes, m => m.RamPercent >= settings.RamThresholdPercent) : null, $"RAM của máy {device.Name} ở mức ít nhất {settings.RamThresholdPercent}% trong {settings.SustainedMinutes} phút."),
                    ("AntivirusProtection", protectionFresh ? ProtectionBreached(latest!.AntivirusStatus) : null, $"Bảo vệ chống mã độc trên máy {device.Name} cần IT kiểm tra."),
                    ("FirewallProtection", protectionFresh ? ProtectionBreached(latest!.FirewallDomainStatus, latest.FirewallPrivateStatus, latest.FirewallPublicStatus) : null, $"Có profile tường lửa của máy {device.Name} chưa được bảo vệ."),
                    ("DiskEncryption", protectionFresh ? ProtectionBreached(latest!.DiskEncryptionStatus) : null, $"Ổ đĩa hệ thống của máy {device.Name} chưa bật bảo vệ mã hóa.")
                };
                foreach (var (key, breached, message) in rules)
                {
                    var active = existing.SingleOrDefault(a => a.AutomaticRule == key && a.IsOpen);
                    if (breached == false && active is not null)
                    {
                        active.Resolve(Guid.Empty, now);
                        active.UpdatedAt = now;
                        alerts.AddAudit(new AuditLog
                        {
                            OrganizationId = organizationId,
                            ActorId = Guid.Empty,
                            DeviceId = device.DeviceId,
                            Action = $"AutomaticAlertRecovered:{key}",
                            Reason = "Điều kiện cảnh báo đã trở lại bình thường",
                            Outcome = "Resolved",
                            CreatedAt = now
                        });
                    }
                    if (breached != true || active is not null || existing.Any(a => a.AutomaticRule == key && a.CreatedAt > now.AddHours(-1))) continue;
                    alerts.Add(new Alert
                    {
                        OrganizationId = organizationId,
                        DeviceId = device.DeviceId,
                        AutomaticRule = key,
                        Severity = key == "DiskHigh" ? "Critical" : "Warning",
                        Message = message,
                        CreatedAt = now
                    });
                    alerts.AddAudit(new AuditLog
                    {
                        OrganizationId = organizationId,
                        ActorId = Guid.Empty,
                        DeviceId = device.DeviceId,
                        Action = $"AutomaticAlertTriggered:{key}",
                        Reason = message,
                        Outcome = "Open",
                        CreatedAt = now
                    });
                    if (key is "AntivirusProtection" or "FirewallProtection" or "DiskEncryption")
                        alerts.AddSecurityEvent(new SecurityEvent
                        {
                            OrganizationId = organizationId,
                            DeviceId = device.DeviceId,
                            Type = key,
                            Summary = message,
                            CreatedAt = now
                        });
                }
                await alerts.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static bool? Sustained(IReadOnlyList<HealthMeasurement> measurements, DateTimeOffset now, int minutes, Func<HealthMeasurement, bool> exceeds)
    {
        var ordered = measurements.Where(m => m.CollectedAt <= now).OrderByDescending(m => m.CollectedAt).ToArray();
        if (ordered.Length == 0 || ordered[0].CollectedAt < now.AddMinutes(-2)) return null;
        for (var i = 0; i < ordered.Length; i++)
        {
            if (i > 0 && ordered[i - 1].CollectedAt - ordered[i].CollectedAt > TimeSpan.FromMinutes(2)) return null;
            if (!exceeds(ordered[i])) return false;
            if (ordered[i].CollectedAt <= now.AddMinutes(-minutes)) return true;
        }
        return null;
    }

    private static bool? ProtectionBreached(params string[] states) => states.Contains(EndpointProtection.Unhealthy) ? true :
        states.All(value => value == EndpointProtection.Healthy) ? false : null;
}
