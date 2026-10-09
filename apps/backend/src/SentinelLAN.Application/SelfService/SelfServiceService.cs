using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed partial class SelfServiceService(ISelfServiceStore store, ISelfServiceDirectory directory,
    ISelfServiceCommands commands, ISelfServiceAudit audit, IMaintenanceCodeProtector codes,
    SelfServiceSettings settings, TimeProvider timeProvider)
{
    private static readonly HashSet<string> Kinds = ["Incident", "InstallApp", "Privilege", "Panic", "PauseAgent", "UninstallAgent", "Appointment"];
    private static readonly HashSet<string> Categories = ["Network", "Printer", "Slow", "Application", "Suspicious", "Other"];
    private static readonly HashSet<string> States = ["Open", "InProgress", "AwaitingEmployee", "Approved", "Rejected", "Resolved", "Closed"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private DateTimeOffset Now => timeProvider.GetUtcNow();
    private static bool IsIt(ActorContext actor) => actor.Role is Roles.Admin;
    private static bool IsMaintenance(SelfServiceRequest request) => request.Kind is "PauseAgent" or "UninstallAgent";
    private static void Require(bool condition, int status, string message)
    {
        if (!condition) throw new SelfServiceException(status, message);
    }
    private static void Human(ActorContext actor) => Require(actor.Role is Roles.Admin or Roles.Employee, 403, "Chỉ tài khoản nhân viên hoặc IT được sử dụng tính năng này.");
    private static void Confirm(string? reason, bool confirmed) => Require(confirmed && !string.IsNullOrWhiteSpace(reason) && reason.Length <= 1000, 400, "Vui lòng nhập lý do và xác nhận thao tác.");
    private async Task<T> MutateAsync<T>(Guid organizationId, Func<Task<T>> action, CancellationToken ct)
    {
        await using var scope = await store.BeginWriteAsync(organizationId, ct);
        var result = await action();
        await store.SaveChangesAsync(ct);
        await scope.CommitAsync(ct);
        return result;
    }
    private async Task<User> ActorUserAsync(ActorContext actor, CancellationToken ct)
    {
        Human(actor);
        var user = await directory.FindUserAsync(actor.OrganizationId, actor.UserId, ct);
        Require(user is { Status: UserStatuses.Active } && user.Role == actor.Role, 403, "Tài khoản không còn hoạt động.");
        return user!;
    }
    private async Task<SelfServiceRequest> ScopedRequestAsync(ActorContext actor, Guid id, CancellationToken ct)
    {
        Human(actor);
        var request = await store.FindRequestAsync(actor.OrganizationId, id, ct);
        Require(request is not null, 404, "Không tìm thấy yêu cầu.");
        if (actor.Role == Roles.Employee)
        {
            var assigned = await directory.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, ct);
            Require(request!.UserId == actor.UserId && assigned is not null && assigned.Id == request.DeviceId, 404, "Không tìm thấy yêu cầu của bạn.");
        }
        return request!;
    }
    private async Task<Device> ActiveTargetAsync(SelfServiceRequest request, CancellationToken ct)
    {
        var device = await directory.FindDeviceAsync(request.OrganizationId, request.DeviceId, ct);
        var owner = await directory.FindUserAsync(request.OrganizationId, request.UserId, ct);
        Require(device is { IsRevoked: false } && device.AssignedUserId == request.UserId && owner is { Status: UserStatuses.Active, Role: Roles.Employee }, 409, "Máy hoặc người dùng đã thay đổi; vui lòng gửi yêu cầu mới.");
        return device!;
    }
    private async Task<SupportRequestDto> DtoAsync(SelfServiceRequest request, CancellationToken ct)
    {
        var commandStatus = request.CommandStatus;
        var commandMessage = request.CommandMessage;
        if (request.CommandId is Guid commandId)
        {
            var (command, result) = await commands.GetAsync(request.OrganizationId, commandId, ct);
            if (command is not null)
            {
                commandStatus = command.ExpiresAt <= Now && command.Status is DeviceCommandStatus.Pending or DeviceCommandStatus.Delivered
                    ? "Expired" : command.Status.ToString();
                commandMessage = result?.Message ?? (commandStatus == "Expired" ? "Máy chưa hoàn tất thao tác trong thời hạn cho phép." : request.CommandMessage);
            }
        }
        return new(request.Id, request.DeviceId, request.DeviceName, request.UserId, request.UserName,
            request.Kind, request.Category, request.Title, request.Description, request.CanWork, request.Status,
            request.AssignedTechnicianId, request.AssignedTechnicianName, request.CatalogAppId, request.CommandId,
            commandStatus, commandMessage, request.AppointmentAt, request.CreatedAt, request.UpdatedAt, request.ApprovalExpiresAt);
    }

    public async Task<IReadOnlyList<SupportRequestDto>> GetRequestsAsync(ActorContext actor, CancellationToken ct)
    {
        Human(actor);
        var requests = await store.GetRequestsAsync(actor.OrganizationId, ct);
        if (actor.Role == Roles.Employee)
        {
            var device = await directory.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, ct);
            requests = requests.Where(r => r.UserId == actor.UserId && r.DeviceId == device?.Id).ToArray();
        }
        var result = new List<SupportRequestDto>();
        foreach (var request in requests.OrderByDescending(r => r.CreatedAt).Take(200)) result.Add(await DtoAsync(request, ct));
        return result;
    }
    public async Task<SupportRequestDto> GetRequestAsync(ActorContext actor, Guid id, CancellationToken ct) => await DtoAsync(await ScopedRequestAsync(actor, id, ct), ct);

    public Task<SupportRequestDto> CreateAsync(ActorContext actor, CreateSupportRequest input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Require(actor.Role == Roles.Employee, 403, "Yêu cầu tự phục vụ được gửi từ tài khoản nhân viên.");
        var user = await ActorUserAsync(actor, ct);
        Require(Kinds.Contains(input.Kind ?? "") && Categories.Contains(input.Category ?? "") &&
            !string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 200 && (input.Description?.Length ?? 0) <= 4000 &&
            IncidentIdempotency.IsValidKey(input.IdempotencyKey), 400, "Vui lòng chọn vấn đề và nhập tiêu đề hợp lệ.");
        Require(input.Kind is "Incident" or "Appointment" || input.Confirmed, 400, "Vui lòng xác nhận thao tác.");
        var device = await directory.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, ct);
        Require(device is not null, 409, "Bạn chưa được phân công máy tính; vui lòng liên hệ IT.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input with { Title = input.Title.Trim(), Description = input.Description?.Trim() }, JsonOptions))));
        var replay = await store.FindIdempotentRequestAsync(actor.OrganizationId, actor.UserId, input.IdempotencyKey, ct);
        if (replay is not null)
        {
            Require(replay.RequestFingerprint == fingerprint && replay.DeviceId == device!.Id, 409, "Mã gửi yêu cầu đã được dùng cho nội dung khác.");
            return await DtoAsync(replay, ct);
        }
        Require(input.Kind == "Appointment" || input.AppointmentAt is null, 400, "Chỉ yêu cầu hẹn IT mới có giờ hẹn.");
        if (input.Kind == "Appointment")
            Require(input.AppointmentAt is not null && input.AppointmentAt > Now.AddMinutes(5) && input.AppointmentAt <= Now.AddDays(30), 400, "Vui lòng chọn giờ hẹn từ 5 phút đến 30 ngày tới.");
        SelfServiceCatalogApp? app = null;
        if (input.Kind is "InstallApp" or "Privilege")
        {
            Require(input.CatalogAppId is not null, 400, "Vui lòng chọn phần mềm được IT cho phép; hệ thống không cấp quyền quản trị toàn máy.");
            app = await store.FindCatalogAppAsync(actor.OrganizationId, input.CatalogAppId!.Value, ct);
            Require(app is { IsActive: true }, 400, "Phần mềm không còn có sẵn.");
        }
        else Require(input.CatalogAppId is null, 400, "Loại yêu cầu này không sử dụng phần mềm trong danh mục.");
        var request = new SelfServiceRequest
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = device!.Id,
            UserId = actor.UserId,
            DeviceName = device.Name,
            UserName = user.DisplayName,
            Kind = input.Kind!,
            Category = input.Category!,
            Title = input.Title.Trim(),
            Description = input.Description?.Trim(),
            CanWork = input.CanWork,
            CatalogAppId = input.CatalogAppId,
            ApprovedPackageJson = app is null ? null : JsonSerializer.Serialize(new ApprovedPackageSnapshot(app.PackageUrl, app.Sha256, app.PublisherThumbprint), JsonOptions),
            AppointmentAt = input.AppointmentAt?.ToUniversalTime(),
            IdempotencyKey = input.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CreatedAt = Now,
            UpdatedAt = Now
        };
        store.Add(request);
        audit.Record(actor.OrganizationId, actor.UserId, device.Id, $"SelfServiceRequested:{request.Kind}", request.Title, "Open");
        await NotifyAsync(actor.OrganizationId, actor.UserId, "IT đã nhận yêu cầu", "Bạn có thể theo dõi và trao đổi với IT trong yêu cầu của mình.", request.Id, $"request:{request.Id}:created", ct);
        await NotifyItAsync(actor.OrganizationId, request.Kind == "Panic" ? "Nhân viên báo khẩn cấp" : "Yêu cầu hỗ trợ mới", "Có yêu cầu cần IT xem xét.", request.Id, $"request:{request.Id}:it", ct);
        if (app is { RequiresApproval: false } && request.Kind == "InstallApp")
        {
            request.ApprovedByUserId = app.PublishedByUserId;
            QueueInstaller(request, app.PublishedByUserId, "Cài phần mềm được IT cho phép", Now.AddMinutes(30));
        }
        if (request.Kind == "Panic")
        {
            audit.CriticalAlert(actor.OrganizationId, device.Id, "Nhân viên nghi máy bị nhiễm mã độc; cần IT kiểm tra ngay.");
            if (settings.PanicLabEnabled && settings.PanicLabDeviceIds.Contains(device.Id))
            {
                var command = commands.Queue(actor.OrganizationId, device.Id, actor.UserId, "IsolateNetwork", request.Title,
                    "30", Now, Now.AddMinutes(2));
                request.CommandId = command.Id;
                request.CommandStatus = "Pending";
                request.CommandMessage = "Đã gửi yêu cầu cô lập thử nghiệm; đang chờ máy xác nhận.";
            }
            else
            {
                request.CommandStatus = "Unavailable";
                request.CommandMessage = "IT đã được báo. Cô lập mạng thật chỉ khả dụng trên máy lab được cho phép.";
            }
        }
        return await DtoAsync(request, ct);
    }, ct);

    public Task<SupportRequestDto> UpdateAsync(ActorContext actor, Guid id, UpdateSupportRequest input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Confirm(input.Reason, input.Confirmed);
        var request = await ScopedRequestAsync(actor, id, ct);
        Require(States.Contains(input.Status ?? ""), 400, "Trạng thái không hợp lệ.");
        if (request.Status == "Approved" && request.CommandId is null)
            Require(input.Status == "Approved", 409, "Cần hoàn tất bước xác nhận riêng trước khi bắt đầu thao tác đã duyệt.");
        Require(SupportRequestTransitions.CanChange(request.Kind, request.Status, input.Status!, actor.Role == Roles.Employee),
            409, "Yêu cầu chưa thể chuyển sang trạng thái này. Hãy tải lại và thực hiện bước xử lý tiếp theo.");
        if (input.Status is "Resolved" or "AwaitingEmployee" or "Closed" && request.CommandId is not null)
        {
            var (command, _) = await commands.GetAsync(actor.OrganizationId, request.CommandId.Value, ct);
            Require(command?.Status == DeviceCommandStatus.Succeeded, 409,
                "Máy chưa xác nhận thao tác hoàn tất. Vui lòng xem trạng thái lệnh trước khi đóng yêu cầu.");
        }
        if (actor.Role == Roles.Employee)
        {
            Require(input.Status is "Closed" or "Open" && input.AssignedTechnicianId is null, 403, "Bạn chỉ có thể xác nhận đã dùng được hoặc báo vẫn còn lỗi.");
            // Reopening an approval request would permit a second authorization on a consumed OTP.
            Require(request.Kind is "Incident" or "Appointment" or "Panic" || input.Status == "Closed", 409, "Vui lòng gửi yêu cầu mới cho thao tác cần phê duyệt.");
        }
        else
        {
            Require(IsIt(actor), 403, "Chỉ IT được cập nhật yêu cầu.");
            Require(input.Status is not ("Approved" or "Rejected") || input.Status == request.Status,
                400, "Vui lòng sử dụng thao tác phê duyệt riêng.");
            if (input.AssignedTechnicianId is Guid technicianId)
            {
                var technician = await directory.FindUserAsync(actor.OrganizationId, technicianId, ct);
                Require(technician is { Status: UserStatuses.Active } && technician.Role is Roles.Admin, 400, "Người phụ trách phải là IT đang hoạt động trong công ty.");
                request.AssignedTechnicianId = technicianId;
                request.AssignedTechnicianName = technician!.DisplayName;
            }
        }
        request.Status = input.Status!;
        request.UpdatedAt = Now;
        audit.Record(actor.OrganizationId, actor.UserId, request.DeviceId, "SelfServiceStatusChanged", input.Reason.Trim(), request.Status);
        await NotifyAsync(actor.OrganizationId, request.UserId, "Yêu cầu của bạn đã được cập nhật", "Mở yêu cầu để xem tiến độ và bước tiếp theo.", request.Id, $"request:{id}:updated:{Guid.NewGuid():N}", ct);
        if (actor.Role == Roles.Employee) await NotifyItAsync(actor.OrganizationId, "Nhân viên cập nhật yêu cầu", "Có phản hồi mới cần IT xem xét.", id, $"request:{id}:employee:{Guid.NewGuid():N}", ct);
        return await DtoAsync(request, ct);
    }, ct);

    private void QueueInstaller(SelfServiceRequest request, Guid actorId, string reason, DateTimeOffset expiresAt)
    {
        Require(request.ApprovedPackageJson is not null, 400, "Cần chọn phần mềm được phép trước khi duyệt.");
        var package = JsonSerializer.Deserialize<ApprovedPackageSnapshot>(request.ApprovedPackageJson!, JsonOptions)!;
        var command = commands.Queue(request.OrganizationId, request.DeviceId, actorId, "InstallApprovedApp", reason,
            JsonSerializer.Serialize(new { requestId = request.Id, package.PackageUrl, package.Sha256, package.PublisherThumbprint, approvalExpiresAt = expiresAt }, JsonOptions), Now, expiresAt);
        request.Status = "Approved";
        request.ApprovalExpiresAt = expiresAt;
        request.CommandId = command.Id;
        request.CommandStatus = "Pending";
        request.CommandMessage = "IT đã duyệt. Đang chờ máy xác nhận kết quả cài đặt.";
    }

    public Task<SupportDecisionDto> DecideAsync(ActorContext actor, Guid id, DecideSupportRequest input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Require(actor.Role == Roles.Admin, 403, "Chỉ quản trị viên được phê duyệt thao tác này.");
        Confirm(input.Reason, input.Confirmed);
        var request = await ScopedRequestAsync(actor, id, ct);
        Require(IsMaintenance(request) || request.Kind is "InstallApp" or "Privilege", 400, "Yêu cầu này không cần phê duyệt quyền hoặc bảo trì.");
        Require(request.ApprovedByUserId is null && request.Status is "Open" or "InProgress" && request.CommandId is null, 409, "Yêu cầu đã được quyết định; vui lòng gửi yêu cầu mới.");
        await ActiveTargetAsync(request, ct);
        request.ApprovedByUserId = actor.UserId;
        request.Status = input.Approved ? "Approved" : "Rejected";
        request.UpdatedAt = Now;
        string? otp = null;
        if (input.Approved)
        {
            if (IsMaintenance(request))
            {
                otp = codes.Generate();
                request.MaintenanceCodeHash = codes.Protect(request, otp);
                request.ApprovalExpiresAt = Now.AddMinutes(5);
            }
            else QueueInstaller(request, actor.UserId, input.Reason.Trim(), Now.AddMinutes(30));
        }
        audit.Record(actor.OrganizationId, actor.UserId, request.DeviceId, $"SelfServiceDecision:{request.Kind}", input.Reason.Trim(), request.Status);
        await NotifyAsync(actor.OrganizationId, request.UserId, input.Approved ? "IT đã duyệt yêu cầu" : "IT chưa chấp nhận yêu cầu", input.Approved && IsMaintenance(request) ? "Liên hệ IT để nhận mã xác nhận; mã có thời hạn 5 phút." : "Mở yêu cầu để xem kết quả và trao đổi với IT.", id, $"request:{id}:decision", ct);
        return new SupportDecisionDto(await DtoAsync(request, ct), otp, input.Approved ? request.ApprovalExpiresAt : null);
    }, ct);

    public async Task<SupportRequestDto> RedeemAsync(ActorContext actor, Guid id, RedeemMaintenanceRequest input, CancellationToken ct)
    {
        Require(actor.Role == Roles.Employee, 403, "Chỉ nhân viên được phân công máy mới được dùng mã này.");
        Require(input.Confirmed, 400, "Vui lòng xác nhận thao tác.");
        return await RedeemCoreAsync(actor.OrganizationId, id, actor.UserId, null, input.Code, ct);
    }
    public Task<SupportRequestDto> RedeemAgentAsync(AgentContext agent, AgentRedeemMaintenanceRequest input, CancellationToken ct) =>
        RedeemCoreAsync(agent.OrganizationId, input.RequestId, null, agent.DeviceId, input.Code, ct);

    private async Task<SupportRequestDto> RedeemCoreAsync(Guid organizationId, Guid id, Guid? userId, Guid? deviceId, string code, CancellationToken ct)
    {
        var outcome = await MutateAsync(organizationId, async () =>
        {
            var request = await store.FindRequestAsync(organizationId, id, ct);
            Require(request is not null && (!userId.HasValue || request.UserId == userId) && (!deviceId.HasValue || request.DeviceId == deviceId), 404, "Không tìm thấy yêu cầu bảo trì.");
            await ActiveTargetAsync(request!, ct);
            Require(IsMaintenance(request!) && request!.Status == "Approved" && request.MaintenanceCodeHash is not null &&
                request.MaintenanceCodeUsedAt is null && request.CommandId is null && request.MaintenanceCodeAttempts < 5 && request.ApprovalExpiresAt > Now, 409, "Mã đã hết hạn, đã dùng hoặc đã bị khóa. Vui lòng gửi yêu cầu mới.");
            request!.MaintenanceCodeAttempts++;
            if (code is null || code.Length != 8 || !code.All(char.IsAsciiDigit) || !codes.Matches(request, code))
            {
                audit.Record(organizationId, request.UserId, request.DeviceId, "MaintenanceCodeRejected", "Mã không hợp lệ", request.MaintenanceCodeAttempts >= 5 ? "Locked" : "Rejected");
                return (Request: (SupportRequestDto?)null, WrongCode: true);
            }
            request.MaintenanceCodeUsedAt = Now;
            var expiresAt = request.ApprovalExpiresAt!.Value;
            var command = commands.Queue(organizationId, request.DeviceId, request.ApprovedByUserId!.Value, request.Kind,
                "Bảo trì đã được IT duyệt và mã xác nhận đã được sử dụng", JsonSerializer.Serialize(new { requestId = request.Id, maintenanceExpiresAt = expiresAt, pauseMinutes = 15 }, JsonOptions), Now, expiresAt);
            request.CommandId = command.Id;
            request.CommandStatus = "Pending";
            request.CommandMessage = "Mã đã được xác nhận. Đang chờ máy thực hiện thao tác.";
            request.MaintenanceUntil = request.Kind == "PauseAgent" ? Now.AddMinutes(15) : expiresAt;
            request.UpdatedAt = Now;
            audit.Record(organizationId, request.UserId, request.DeviceId, "MaintenanceCodeRedeemed", "Xác nhận bảo trì có phê duyệt", "CommandPending");
            await NotifyAsync(organizationId, request.UserId, "Đã xác nhận yêu cầu bảo trì", "Đang chờ máy báo kết quả; việc phê duyệt chưa có nghĩa thao tác đã hoàn tất.", request.Id, $"request:{id}:redeemed", ct);
            return (Request: (SupportRequestDto?)await DtoAsync(request, ct), WrongCode: false);
        }, ct);
        Require(!outcome.WrongCode, 400, "Mã xác nhận không đúng. Sau 5 lần sai, bạn cần gửi yêu cầu mới.");
        return outcome.Request!;
    }
}
