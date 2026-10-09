namespace SentinelLAN.Application;

public sealed class VpsHostMonitorService(
    IVpsHostSnapshotReader reader,
    IAuthenticationStore authentication,
    VpsHostMonitorSettings settings,
    TimeProvider clock)
{
    public async Task<VpsHostStatusDto> GetStatusAsync(ActorContext actor, CancellationToken ct)
    {
        if (actor.Role != Roles.Admin) throw new UnauthorizedAccessException();
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(settings.OrganizationCode))
            return new(false, false, false, "Chưa cấu hình đơn vị vận hành VPS.", null);
        var organization = await authentication.FindOrganizationAsync(actor.OrganizationId, ct);
        if (organization is null || !string.Equals(organization.Code, settings.OrganizationCode, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException();
        if (!reader.Configured)
            return new(false, false, false, "Chưa bật kiểm tra VPS đang chạy SentinelLAN.", null);
        var snapshot = await reader.ReadAsync(ct);
        if (snapshot is null)
            return new(true, false, true, "Chưa đọc được trạng thái VPS. Kiểm tra bộ thu thập trên máy chủ.", null);
        var age = clock.GetUtcNow() - snapshot.CapturedAtUtc;
        var stale = age > TimeSpan.FromMinutes(3) || age < TimeSpan.FromMinutes(-1);
        return new(true, true, stale, stale ? "Dữ liệu chưa được cập nhật. Đây là lần kiểm tra gần nhất." : "Đã đọc trạng thái VPS.", snapshot);
    }
}
