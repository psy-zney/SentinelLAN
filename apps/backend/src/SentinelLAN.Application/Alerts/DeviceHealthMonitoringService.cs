using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class DeviceHealthMonitoringService(IDeviceHealthReader devices, IAutomaticAlertStore alerts, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        foreach (var organizationId in await devices.GetActiveOrganizationsAsync(cancellationToken))
        {
            foreach (var device in await devices.GetDevicesAsync(organizationId, now, cancellationToken))
            {
                var existing = await alerts.GetRecentAsync(organizationId, device.DeviceId, now.AddHours(-1), cancellationToken);
                var latest = device.Measurements.OrderByDescending(m => m.CollectedAt).FirstOrDefault();
                var fresh = latest is not null && latest.CollectedAt >= now.AddMinutes(-2) && latest.CollectedAt <= now;
                // Null means the condition cannot currently be evaluated; do not call missing telemetry a recovery.
                var rules = new (string Key, bool? Breached, string Message)[]
                {
                    ("Offline", device.LastSeenAt is null ? null : device.MaintenanceUntil > now ? false : device.LastSeenAt <= now.AddMinutes(-5), $"Máy {device.Name} đã mất kết nối ít nhất 5 phút. Kiểm tra nguồn điện và mạng."),
                    ("DiskHigh", fresh ? latest!.DiskPercent >= 95 : null, $"Ổ đĩa hệ thống của máy {device.Name} đã sử dụng ít nhất 95%. IT cần kiểm tra dung lượng."),
                    ("CpuHigh", fresh ? Sustained(device.Measurements, now, m => m.CpuPercent >= 90) : null, $"CPU của máy {device.Name} ở mức ít nhất 90% liên tục trong 5 phút."),
                    ("RamHigh", fresh ? Sustained(device.Measurements, now, m => m.RamPercent >= 90) : null, $"RAM của máy {device.Name} ở mức ít nhất 90% liên tục trong 5 phút.")
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
                }
                await alerts.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static bool? Sustained(IReadOnlyList<HealthMeasurement> measurements, DateTimeOffset now, Func<HealthMeasurement, bool> exceeds)
    {
        var ordered = measurements.Where(m => m.CollectedAt <= now).OrderByDescending(m => m.CollectedAt).ToArray();
        if (ordered.Length == 0 || ordered[0].CollectedAt < now.AddMinutes(-2)) return null;
        for (var i = 0; i < ordered.Length; i++)
        {
            if (i > 0 && ordered[i - 1].CollectedAt - ordered[i].CollectedAt > TimeSpan.FromMinutes(2)) return null;
            if (!exceeds(ordered[i])) return false;
            if (ordered[i].CollectedAt <= now.AddMinutes(-5)) return true;
        }
        return null;
    }
}
