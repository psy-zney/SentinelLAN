using System.Globalization;
using System.Text.Json;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IPreventiveDeviceReader
{
    Task<IReadOnlyList<Device>> GetDevicesAsync(Guid organizationId, CancellationToken cancellationToken);
}
public interface IPreventiveMaintenanceStore
{
    Task<bool> HasRunAsync(Guid organizationId, DateOnly day, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkOrder>> GetPreventiveOrdersAsync(Guid organizationId, CancellationToken cancellationToken);
    void Add(WorkOrder order);
    void AddRun(PreventiveMaintenanceRun run);
    void Audit(AuditLog audit);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}

public sealed class PreventiveMaintenanceService(IDeviceHealthReader organizations, IPreventiveDeviceReader devices,
    IPreventiveMaintenanceStore store, IMonitoringSettingsStore settingsStore, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        foreach (var org in await organizations.GetActiveOrganizationsAsync(ct))
        {
            if (await store.HasRunAsync(org, day, ct)) continue;
            var settings = MonitoringSettingsService.ToDto(await settingsStore.FindAsync(org, ct));
            var existing = await store.GetPreventiveOrdersAsync(org, ct);
            foreach (var device in await devices.GetDevicesAsync(org, ct))
            {
                if (existing.Any(value => value.DeviceId == device.Id && value.Status is not ("Completed" or "Cancelled"))) continue;
                var last = existing.Where(value => value.DeviceId == device.Id && value.CompletedAt is not null)
                    .Select(value => value.CompletedAt!.Value).DefaultIfEmpty(device.PurchaseDate ?? device.CreatedAt).Max();
                var due = last.AddMonths(settings.PreventiveIntervalMonths);
                var warrantySoon = device.WarrantyExpiresAt is { } warranty && warranty >= now && warranty <= now.AddDays(settings.WarrantyNoticeDays);
                if (due > now && !warrantySoon) continue;
                var key = warrantySoon
                    ? $"warranty:{device.Id:N}:{device.WarrantyExpiresAt!.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}"
                    : $"periodic:{device.Id:N}:{due.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
                if (existing.Any(value => value.AutomaticKey == key)) continue;
                var reason = warrantySoon ? "Kiểm tra máy trước khi hết bảo hành" : "Bảo dưỡng định kỳ đã đến hạn";
                var order = new WorkOrder
                {
                    OrganizationId = org,
                    DeviceId = device.Id,
                    Type = "Preventive",
                    Status = "Scheduled",
                    AutomaticKey = key,
                    WorkOrderNumber = $"AUTO-{day:yyyyMMdd}-{device.Id:N}",
                    Title = $"{reason}: {device.Name}",
                    DueDate = due <= now ? due : device.WarrantyExpiresAt,
                    Notes = "Phiếu được tạo tự động theo cấu hình đơn vị. IT kiểm tra máy và xác nhận hoàn tất; hệ thống không tự thay đổi thiết bị.",
                    ChecklistJson = JsonSerializer.Serialize(new[] { "Kiểm tra tình trạng phần cứng và dung lượng", "Kiểm tra trạng thái bảo vệ máy", "Kiểm tra lịch sao lưu và thời hạn bảo hành" }),
                    CreatedAt = now
                };
                store.Add(order);
                store.Audit(new AuditLog
                {
                    OrganizationId = org,
                    DeviceId = device.Id,
                    ActorId = Guid.Empty,
                    Action = "PreventiveWorkOrderCreated",
                    Reason = reason,
                    Outcome = "Scheduled",
                    CreatedAt = now
                });
            }
            store.AddRun(new PreventiveMaintenanceRun { OrganizationId = org, Day = day, CreatedAt = now });
            if (!await store.TrySaveAsync(ct)) return; // Another replica won; its complete transaction owns this day.
        }
    }
}
