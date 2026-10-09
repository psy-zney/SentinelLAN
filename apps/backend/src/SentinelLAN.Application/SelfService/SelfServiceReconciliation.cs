using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed partial class SelfServiceService
{
    public async Task ReconcileAsync(CancellationToken ct)
    {
        foreach (var organizationId in await directory.GetOrganizationIdsAsync(ct))
            await MutateAsync(organizationId, async () =>
            {
                var requests = await store.GetRequestsAsync(organizationId, ct);
                foreach (var request in requests.Where(r => r.CommandId is not null))
                {
                    var (command, result) = await commands.GetAsync(organizationId, request.CommandId!.Value, ct);
                    if (command is null) continue;
                    var status = command.EffectiveStatus(Now).ToString();
                    var message = result?.Message ?? (status == "Expired"
                        ? "Lệnh đã hết hạn trước khi được giao cho máy."
                        : status == "ExecutionUnconfirmed" ? "Lệnh đã được giao nhưng chưa nhận được xác nhận. IT cần kiểm tra trước khi thực hiện lại."
                        : "Đang chờ máy báo kết quả.");
                    if (request.CommandStatus == status && request.CommandMessage == message) continue;
                    request.CommandStatus = status;
                    request.CommandMessage = message;
                    request.UpdatedAt = Now;
                    if (status is not ("Succeeded" or "Failed" or "Expired" or "ExecutionUnconfirmed")) continue;
                    if (status == "Succeeded" && request.Status is not ("Closed" or "Resolved")) request.Status = "AwaitingEmployee";
                    var title = status == "Succeeded" ? "Máy đã báo hoàn tất thao tác"
                        : status == "ExecutionUnconfirmed" ? "Chưa xác nhận kết quả thao tác" : "Thao tác trên máy chưa hoàn tất";
                    await NotifyAsync(organizationId, request.UserId, title, "Mở yêu cầu để xem kết quả và xác nhận với IT.", request.Id, $"command:{command.Id}:{status}", ct);
                    await NotifyItAsync(organizationId, title, "Có kết quả thao tác cần IT theo dõi.", request.Id, $"command:{command.Id}:{status}", ct);
                    audit.Record(organizationId, command.IssuedByUserId, request.DeviceId, "SelfServiceCommandReconciled", "Kết quả được Agent gửi hoặc lệnh đã hết hạn", status);
                }
                foreach (var announcement in (await store.GetAnnouncementsAsync(organizationId, ct)).Where(a => a.PublishedAt is null && a.StartsAt <= Now && a.EndsAt > Now))
                    await PublishAnnouncementAsync(announcement, ct);
                foreach (var device in (await directory.GetDevicesAsync(organizationId, ct)).Where(d => !d.IsRevoked && d.AssignedUserId is not null && d.LastSeenAt is not null && !d.IsOnline(Now)))
                {
                    var expected = requests.Any(r => r.DeviceId == device.Id && r.MaintenanceCodeUsedAt is not null &&
                        r.MaintenanceUntil > Now && r.CommandStatus is "Delivered" or "Succeeded" &&
                        (r.Kind == "UninstallAgent" || r.Kind == "PauseAgent" && device.MaintenanceAction == "PauseAgent" && device.MaintenanceUntil > Now));
                    if (expected) continue;
                    var key = $"offline:{device.Id}:{device.LastSeenAt!.Value.UtcTicks}";
                    await NotifyAsync(organizationId, device.AssignedUserId!.Value, "Máy của bạn đang mất kết nối", "SentinelLAN chưa nhận được trạng thái từ máy. Máy có thể đã tắt hoặc mất mạng; IT đã được thông báo.", null, key, ct);
                    await NotifyItAsync(organizationId, "Máy nhân viên đang mất kết nối", "Chưa nhận được heartbeat; đây không phải bằng chứng Agent bị cố ý kết thúc.", null, key, ct);
                }
                return true;
            }, ct);
    }
}
