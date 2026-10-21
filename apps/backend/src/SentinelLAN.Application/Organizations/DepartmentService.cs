using System.Security.Cryptography;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record AdministrativeConfirmation(string Reason, bool Confirmed, Guid Nonce, DateTimeOffset ExpiresAt);
public sealed record DepartmentDto(Guid Id, string Name, Guid? PolicyId, Guid Revision, IReadOnlyList<Guid> UserIds, IReadOnlyList<Guid> DeviceIds);
public sealed record DepartmentDirectoryDto(IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<DepartmentMemberDto> Users, IReadOnlyList<DepartmentMemberDto> Devices);
public sealed record DepartmentMemberDto(Guid Id, string Name, Guid? DepartmentId);
public sealed record SaveDepartmentRequest(string Name, Guid? PolicyId, Guid Revision, AdministrativeConfirmation Confirmation);
public sealed record DepartmentMembershipRequest(Guid? DepartmentId, Guid Revision, AdministrativeConfirmation Confirmation);
public sealed record BulkCommandRequest(IReadOnlyList<Guid> DeviceIds, string Type, AdministrativeConfirmation Confirmation);
public sealed record BulkCommandResult(Guid DeviceId, string Outcome, Guid? CommandId);
public interface IDepartmentPolicyReader
{
    Task<Guid?> GetPolicyIdAsync(Guid organizationId, Guid deviceId, CancellationToken ct);
}
public interface IDepartmentStore
{
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(Guid org, CancellationToken ct);
    Task<IReadOnlyList<DepartmentMemberDto>> GetUsersAsync(Guid org, CancellationToken ct);
    Task<IReadOnlyList<Device>> GetDevicesAsync(Guid org, CancellationToken ct);
    Task<User?> FindUserAsync(Guid org, Guid id, CancellationToken ct);
    Task<Device?> FindDeviceAsync(Guid org, Guid id, CancellationToken ct);
    void Add(Department value);
    void Add(DeviceCommand value);
}

public sealed class DepartmentService(IDepartmentStore store, IPolicyStore policies, ICommandSigner signer, IAdministrativeRequestJournal journal, TimeProvider clock)
{
    public async Task<DepartmentDirectoryDto> DirectoryAsync(ActorContext actor, CancellationToken ct)
    {
        Admin(actor);
        var users = await store.GetUsersAsync(actor.OrganizationId, ct);
        var devices = await store.GetDevicesAsync(actor.OrganizationId, ct);
        var departments = await store.GetDepartmentsAsync(actor.OrganizationId, ct);
        return new(departments.OrderBy(x => x.Name).Select(d => new DepartmentDto(d.Id, d.Name, d.PolicyId, d.Revision,
            users.Where(x => x.DepartmentId == d.Id).Select(x => x.Id).ToArray(), devices.Where(x => x.DepartmentId == d.Id).Select(x => x.Id).ToArray())).ToArray(),
            users, devices.Select(d => new DepartmentMemberDto(d.Id, d.Name, d.DepartmentId)).ToArray());
    }
    public async Task SaveAsync(ActorContext actor, Guid? id, SaveDepartmentRequest request, CancellationToken ct)
    {
        await ConfirmAsync(actor, request.Confirmation, ct);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 || request.Name.Any(char.IsControl)) throw new OrganizationConfigurationException(400, "Tên phòng ban không hợp lệ.");
        var departments = await store.GetDepartmentsAsync(actor.OrganizationId, ct);
        var department = departments.SingleOrDefault(x => x.Id == id);
        if (id is not null && department is null) throw new OrganizationConfigurationException(404, "Không tìm thấy phòng ban.");
        if ((department?.Revision ?? Guid.Empty) != request.Revision) throw new OrganizationConfigurationException(409, "Phòng ban đã thay đổi. Hãy tải lại.");
        if (departments.Any(x => x.Id != id && string.Equals(x.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new OrganizationConfigurationException(409, "Tên phòng ban đã tồn tại.");
        if (request.PolicyId is { } policy && await policies.FindPolicyAsync(actor.OrganizationId, policy, ct) is null) throw new OrganizationConfigurationException(400, "Chính sách không thuộc đơn vị.");
        if (department is null) { department = new() { OrganizationId = actor.OrganizationId, Name = "" }; store.Add(department); }
        var changedPolicy = department.PolicyId != request.PolicyId;
        department.Name = request.Name.Trim(); department.PolicyId = request.PolicyId; department.Revision = Guid.NewGuid(); department.UpdatedAt = clock.GetUtcNow();
        if (changedPolicy && department.PolicyId is not null)
        {
            var members = (await store.GetDevicesAsync(actor.OrganizationId, ct)).Where(d => d.DepartmentId == department.Id && !d.IsRevoked).ToArray();
            if (members.Length > 1000) throw new OrganizationConfigurationException(400, "Phòng ban vượt giới hạn 1000 máy. Hãy chia thành các phòng ban nhỏ hơn.");
            foreach (var member in members) QueueRefresh(actor, member.Id, request.Confirmation.Reason);
        }
        await CommitAsync(actor, request.Confirmation, "DepartmentSaved", null, ct);
    }
    public async Task AssignAsync(ActorContext actor, string kind, Guid memberId, DepartmentMembershipRequest request, CancellationToken ct)
    {
        await ConfirmAsync(actor, request.Confirmation, ct);
        if (request.DepartmentId is { } deptId)
        {
            var department = (await store.GetDepartmentsAsync(actor.OrganizationId, ct)).SingleOrDefault(x => x.Id == deptId);
            if (department is null) throw new OrganizationConfigurationException(404, "Phòng ban không thuộc đơn vị.");
            if (department.Revision != request.Revision) throw new OrganizationConfigurationException(409, "Phòng ban đã thay đổi. Hãy tải lại.");
        }
        if (kind == "users")
        {
            var user = await store.FindUserAsync(actor.OrganizationId, memberId, ct) ?? throw new OrganizationConfigurationException(404, "Không tìm thấy nhân viên.");
            user.DepartmentId = request.DepartmentId; user.UpdatedAt = clock.GetUtcNow();
        }
        else
        {
            var device = await store.FindDeviceAsync(actor.OrganizationId, memberId, ct) ?? throw new OrganizationConfigurationException(404, "Không tìm thấy thiết bị.");
            if (device.IsRevoked) throw new OrganizationConfigurationException(409, "Máy đã bị thu hồi.");
            device.DepartmentId = request.DepartmentId; device.UpdatedAt = clock.GetUtcNow();
            if (request.DepartmentId is { } target && (await store.GetDepartmentsAsync(actor.OrganizationId, ct)).Any(d => d.Id == target && d.PolicyId != null))
                QueueRefresh(actor, device.Id, request.Confirmation.Reason);
        }
        await CommitAsync(actor, request.Confirmation, "DepartmentMembershipChanged", kind == "devices" ? memberId : null, ct);
    }
    public async Task<IReadOnlyList<BulkCommandResult>> BulkAsync(ActorContext actor, BulkCommandRequest request, CancellationToken ct)
    {
        await ConfirmAsync(actor, request.Confirmation, ct);
        if (request.Type is not ("RefreshPolicy" or "CollectTelemetryNow") || request.DeviceIds is null || request.DeviceIds.Count is < 1 or > 100 || request.DeviceIds.Distinct().Count() != request.DeviceIds.Count)
            throw new OrganizationConfigurationException(400, "Chọn 1–100 máy và lệnh cập nhật chính sách hoặc thông tin.");
        var results = new List<BulkCommandResult>(); var now = clock.GetUtcNow();
        foreach (var id in request.DeviceIds)
        {
            var device = await store.FindDeviceAsync(actor.OrganizationId, id, ct);
            if (device is null || device.IsRevoked) { results.Add(new(id, device is null ? "Unavailable" : "Revoked", null)); continue; }
            var command = new DeviceCommand
            {
                OrganizationId = actor.OrganizationId,
                DeviceId = id,
                IssuedByUserId = actor.UserId,
                Type = request.Type,
                Reason = request.Confirmation.Reason.Trim(),
                Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                Signature = "pending",
                IssuedAt = now,
                ExpiresAt = now.AddMinutes(2),
                Status = DeviceCommandStatus.Pending
            };
            command.Signature = signer.Sign(command); store.Add(command);
            journal.Audit(new AuditLog { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, DeviceId = id, Action = $"CommandCreated:{request.Type}", Reason = command.Reason, Outcome = "Pending", CreatedAt = now });
            results.Add(new(id, "Queued", command.Id));
        }
        await CommitAsync(actor, request.Confirmation, "BulkCommandsCreated", null, ct);
        return results;
    }
    private static void Admin(ActorContext actor) { if (actor.Role != Roles.Admin) throw new OrganizationConfigurationException(403, "Chỉ Admin được quản lý phòng ban."); }
    private async Task ConfirmAsync(ActorContext actor, AdministrativeConfirmation input, CancellationToken ct)
    {
        Admin(actor); var now = clock.GetUtcNow();
        if (input is null || !input.Confirmed || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 3 or > 1000 || input.Nonce == Guid.Empty || input.ExpiresAt <= now || input.ExpiresAt > now.AddMinutes(5))
            throw new OrganizationConfigurationException(400, "Cần lý do, xác nhận và nonce còn hạn tối đa 5 phút.");
        if (await journal.NonceExistsAsync(actor.OrganizationId, input.Nonce, ct)) throw new OrganizationConfigurationException(409, "Thao tác đã được xử lý.");
    }
    private async Task CommitAsync(ActorContext actor, AdministrativeConfirmation input, string operation, Guid? deviceId, CancellationToken ct)
    {
        journal.Reserve(new AdministrativeRequestNonce { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, Nonce = input.Nonce, Operation = operation, ExpiresAt = input.ExpiresAt });
        journal.Audit(new AuditLog { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, DeviceId = deviceId, Action = operation, Reason = input.Reason.Trim(), Outcome = "Saved" });
        if (!await journal.TrySaveAsync(ct)) throw new OrganizationConfigurationException(409, "Có thao tác đồng thời. Vui lòng tải lại.");
    }
    private void QueueRefresh(ActorContext actor, Guid device, string reason)
    {
        var now = clock.GetUtcNow();
        var command = new DeviceCommand
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = device,
            IssuedByUserId = actor.UserId,
            Type = "RefreshPolicy",
            Reason = reason.Trim(),
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            Signature = "pending",
            IssuedAt = now,
            ExpiresAt = now.AddMinutes(2),
            Status = DeviceCommandStatus.Pending
        };
        command.Signature = signer.Sign(command); store.Add(command);
        journal.Audit(new AuditLog { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, DeviceId = device, Action = "CommandCreated:RefreshPolicy", Reason = reason.Trim(), Outcome = "Pending" });
    }
}
