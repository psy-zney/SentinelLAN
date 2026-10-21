using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class MonitoringSettingsService(IMonitoringSettingsStore store, TimeProvider clock)
{
    public async Task<MonitoringSettingsDto> GetAsync(ActorContext actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return ToDto(await store.FindAsync(actor.OrganizationId, ct));
    }

    public async Task<MonitoringSettingsDto> UpdateAsync(ActorContext actor, UpdateMonitoringSettingsRequest input, CancellationToken ct)
    {
        RequireAdmin(actor);
        var now = clock.GetUtcNow();
        if (!input.Confirmed || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 3 or > 1000 ||
            input.Nonce == Guid.Empty || input.ExpiresAt <= now || input.ExpiresAt > now.AddMinutes(5))
            throw new OrganizationConfigurationException(400, "Cần lý do, xác nhận và mã thao tác còn hạn tối đa 5 phút.");
        var value = input.Settings;
        if (value is null || value.CpuThresholdPercent is < 1 or > 100 || value.RamThresholdPercent is < 1 or > 100 ||
            value.DiskThresholdPercent is < 1 or > 100 || value.OfflineMinutes is < 2 or > 1440 ||
            value.SustainedMinutes is < 0 or > 30 || value.MaintenancePauseMinutes is < 5 or > 60 ||
            value.PreventiveIntervalMonths is < 1 or > 60 || value.WarrantyNoticeDays is < 1 or > 365)
            throw new OrganizationConfigurationException(400, "Ngưỡng cần trong 1–100%, mất kết nối 2–1440 phút, duy trì 0–30 phút và tạm dừng 5–60 phút.");
        if (await store.NonceExistsAsync(actor.OrganizationId, input.Nonce, ct))
            throw new OrganizationConfigurationException(409, "Thao tác này đã được xử lý. Vui lòng tải lại cấu hình.");
        var settings = await store.FindAsync(actor.OrganizationId, ct);
        if ((settings?.Revision ?? Guid.Empty) != value.Revision)
            throw new OrganizationConfigurationException(409, "Cấu hình đã thay đổi. Vui lòng tải lại trước khi lưu.");
        if (settings is null)
        {
            settings = new OrganizationMonitoringSettings { OrganizationId = actor.OrganizationId };
            store.Add(settings);
        }
        settings.CpuThresholdPercent = value.CpuThresholdPercent;
        settings.RamThresholdPercent = value.RamThresholdPercent;
        settings.DiskThresholdPercent = value.DiskThresholdPercent;
        settings.OfflineMinutes = value.OfflineMinutes;
        settings.SustainedMinutes = value.SustainedMinutes;
        settings.MaintenancePauseMinutes = value.MaintenancePauseMinutes;
        settings.PreventiveIntervalMonths = value.PreventiveIntervalMonths;
        settings.WarrantyNoticeDays = value.WarrantyNoticeDays;
        settings.SecurityAlertsEnabled = value.SecurityAlertsEnabled;
        settings.Revision = Guid.NewGuid();
        settings.UpdatedAt = now;
        store.Reserve(new AdministrativeRequestNonce
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            Nonce = input.Nonce,
            Operation = "MonitoringSettingsUpdated",
            ExpiresAt = input.ExpiresAt,
            CreatedAt = now
        });
        store.Audit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            Action = "MonitoringSettingsUpdated",
            Reason = input.Reason.Trim(),
            Outcome = "Saved",
            CreatedAt = now
        });
        if (!await store.TrySaveAsync(ct))
            throw new OrganizationConfigurationException(409, "Cấu hình vừa được cập nhật bởi thao tác khác. Vui lòng tải lại.");
        return ToDto(settings);
    }

    public static MonitoringSettingsDto ToDto(OrganizationMonitoringSettings? value) => value is null ? new() :
        new(value.CpuThresholdPercent, value.RamThresholdPercent, value.DiskThresholdPercent, value.OfflineMinutes,
            value.SustainedMinutes, value.MaintenancePauseMinutes, value.PreventiveIntervalMonths, value.WarrantyNoticeDays,
            value.SecurityAlertsEnabled)
        { Revision = value.Revision };

    private static void RequireAdmin(ActorContext actor)
    {
        if (actor.Role != Roles.Admin || actor.OrganizationId == Guid.Empty) throw new UnauthorizedAccessException();
    }
}
