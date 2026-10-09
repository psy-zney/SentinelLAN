using System.Text.RegularExpressions;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed partial class SelfServiceService
{
    public async Task<IReadOnlyList<SupportMessageDto>> GetMessagesAsync(ActorContext actor, Guid id, CancellationToken ct)
    {
        await ScopedRequestAsync(actor, id, ct);
        return (await store.GetMessagesAsync(actor.OrganizationId, id, ct)).OrderBy(m => m.CreatedAt).Select(MessageDto).ToArray();
    }
    private static SupportMessageDto MessageDto(SelfServiceMessage message) => new(message.Id, message.RequestId, message.AuthorId, message.AuthorName, message.Body, message.CreatedAt);
    public Task<SupportMessageDto> AddMessageAsync(ActorContext actor, Guid id, CreateSupportMessage input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        var request = await ScopedRequestAsync(actor, id, ct);
        var user = await ActorUserAsync(actor, ct);
        Require(!string.IsNullOrWhiteSpace(input.Body) && input.Body.Length <= 4000 && IncidentIdempotency.IsValidKey(input.IdempotencyKey), 400, "Tin nhắn cần có nội dung và dài tối đa 4000 ký tự.");
        var messages = await store.GetMessagesAsync(actor.OrganizationId, id, ct);
        var replay = messages.SingleOrDefault(m => m.AuthorId == actor.UserId && m.IdempotencyKey == input.IdempotencyKey);
        if (replay is not null)
        {
            Require(replay.Body == input.Body.Trim(), 409, "Mã gửi tin nhắn đã được dùng cho nội dung khác.");
            return MessageDto(replay);
        }
        var message = new SelfServiceMessage
        {
            OrganizationId = actor.OrganizationId,
            RequestId = id,
            AuthorId = actor.UserId,
            AuthorName = user.DisplayName,
            Body = input.Body.Trim(),
            IdempotencyKey = input.IdempotencyKey,
            CreatedAt = Now
        };
        store.Add(message);
        request.UpdatedAt = Now;
        if (actor.Role == Roles.Employee)
            await NotifyItAsync(actor.OrganizationId, "Tin nhắn hỗ trợ mới", "Có phản hồi từ nhân viên trong yêu cầu hỗ trợ.", id, $"message:{message.Id}", ct);
        else
            await NotifyAsync(actor.OrganizationId, request.UserId, "IT đã phản hồi", "Mở yêu cầu để đọc tin nhắn từ IT.", id, $"message:{message.Id}", ct);
        return MessageDto(message);
    }, ct);

    private static SupportAttachmentDto AttachmentDto(SelfServiceAttachment attachment) => new(attachment.Id, attachment.RequestId, attachment.FileName, attachment.ContentType, attachment.Content.Length, attachment.CreatedAt);
    public async Task<IReadOnlyList<SupportAttachmentDto>> GetAttachmentsAsync(ActorContext actor, Guid id, CancellationToken ct)
    {
        await ScopedRequestAsync(actor, id, ct);
        return (await store.GetAttachmentsAsync(actor.OrganizationId, id, ct)).OrderBy(a => a.CreatedAt).Select(AttachmentDto).ToArray();
    }
    public async Task<SelfServiceAttachment> DownloadAttachmentAsync(ActorContext actor, Guid id, Guid attachmentId, CancellationToken ct)
    {
        await ScopedRequestAsync(actor, id, ct);
        var attachment = await store.FindAttachmentAsync(actor.OrganizationId, id, attachmentId, ct);
        Require(attachment is not null, 404, "Không tìm thấy ảnh đính kèm.");
        return attachment!;
    }
    public Task<SupportAttachmentDto> AddAttachmentAsync(ActorContext actor, Guid id, CreateSupportAttachment input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        await ScopedRequestAsync(actor, id, ct);
        Require(!string.IsNullOrWhiteSpace(input.FileName) && input.FileName.Length <= 120 && !input.FileName.Any(c => char.IsControl(c) || c is '/' or '\\') &&
            input.ContentType is "image/png" or "image/jpeg" && input.Base64 is not null && input.Base64.Length <= 2_796_204, 400, "Chỉ nhận ảnh PNG hoặc JPEG tối đa 2 MiB; tên ảnh không được chứa đường dẫn.");
        byte[] content;
        try { content = Convert.FromBase64String(input.Base64!); }
        catch (FormatException) { throw new SelfServiceException(400, "Ảnh không hợp lệ."); }
        var png = content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = content.Length >= 4 && content[0] == 255 && content[1] == 216 && content[2] == 255;
        Require(content.Length is > 0 and <= 2 * 1024 * 1024 && (input.ContentType == "image/png" ? png : jpeg), 400, "Nội dung ảnh không đúng định dạng hoặc vượt quá 2 MiB.");
        Require((await store.GetAttachmentsAsync(actor.OrganizationId, id, ct)).Count < 10, 400, "Mỗi yêu cầu nhận tối đa 10 ảnh.");
        var attachment = new SelfServiceAttachment
        {
            OrganizationId = actor.OrganizationId,
            RequestId = id,
            UploadedByUserId = actor.UserId,
            FileName = input.FileName.Trim(),
            ContentType = input.ContentType,
            Content = content,
            CreatedAt = Now
        };
        store.Add(attachment);
        audit.Record(actor.OrganizationId, actor.UserId, null, "SupportImageAttached", "Nhân viên hoặc IT chủ động gửi ảnh lỗi", "Created");
        return AttachmentDto(attachment);
    }, ct);

    private static CatalogAppDto CatalogDto(SelfServiceCatalogApp app) => new(app.Id, app.Name, app.Description, app.Version, app.PackageUrl, app.Sha256, app.PublisherThumbprint, app.IsActive, app.RequiresApproval, app.CreatedAt);
    public async Task<IReadOnlyList<CatalogAppDto>> GetCatalogAsync(ActorContext actor, CancellationToken ct)
    {
        Human(actor);
        return (await store.GetCatalogAsync(actor.OrganizationId, ct)).Where(a => actor.Role != Roles.Employee || a.IsActive).OrderBy(a => a.Name).Select(CatalogDto).ToArray();
    }
    public Task<CatalogAppDto> SaveCatalogAsync(ActorContext actor, Guid? id, SaveCatalogApp input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Require(actor.Role == Roles.Admin, 403, "Chỉ quản trị viên được quản lý danh mục phần mềm.");
        Confirm(input.Reason, input.Confirmed);
        Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 120 && !string.IsNullOrWhiteSpace(input.Description) && input.Description.Length <= 1000 &&
            !string.IsNullOrWhiteSpace(input.Version) && input.Version.Length <= 60, 400, "Vui lòng nhập tên, mô tả và phiên bản phần mềm.");
        Require(input.PackageUrl is { Length: <= 2000 } && Uri.TryCreate(input.PackageUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(url.UserInfo) && string.IsNullOrEmpty(url.Fragment) && url.Port == 443 &&
            Uri.UnescapeDataString(url.AbsolutePath).EndsWith(".msi", StringComparison.OrdinalIgnoreCase), 400, "Gói cài phải là tệp MSI qua HTTPS, không có thông tin đăng nhập hoặc địa chỉ lệnh.");
        Require(input.Sha256 is not null && Regex.IsMatch(input.Sha256, "\\A[0-9a-fA-F]{64}\\z", RegexOptions.CultureInvariant) && input.PublisherThumbprint is not null &&
            Regex.IsMatch(input.PublisherThumbprint, "\\A[0-9a-fA-F]{40}\\z", RegexOptions.CultureInvariant), 400, "Cần mã SHA-256 và định danh chứng thư nhà phát hành hợp lệ.");
        SelfServiceCatalogApp app;
        if (id.HasValue)
        {
            var existing = await store.FindCatalogAppAsync(actor.OrganizationId, id.Value, ct);
            Require(existing is not null, 404, "Không tìm thấy phần mềm.");
            app = existing!;
        }
        else
        {
            app = new SelfServiceCatalogApp
            {
                OrganizationId = actor.OrganizationId,
                PublishedByUserId = actor.UserId,
                Name = "",
                Description = "",
                Version = "",
                PackageUrl = "",
                Sha256 = "",
                PublisherThumbprint = "",
                CreatedAt = Now
            };
            store.Add(app);
        }
        app.Name = input.Name.Trim(); app.Description = input.Description.Trim(); app.Version = input.Version.Trim();
        app.PackageUrl = input.PackageUrl!; app.Sha256 = input.Sha256!.ToUpperInvariant(); app.PublisherThumbprint = input.PublisherThumbprint!.ToUpperInvariant();
        app.IsActive = input.IsActive; app.RequiresApproval = input.RequiresApproval; app.PublishedByUserId = actor.UserId; app.UpdatedAt = Now;
        audit.Record(actor.OrganizationId, actor.UserId, null, id.HasValue ? "CatalogAppUpdated" : "CatalogAppPublished", input.Reason.Trim(), "Saved");
        return CatalogDto(app);
    }, ct);

    public async Task<IReadOnlyList<SelfServiceTeamMemberDto>> GetTeamAsync(ActorContext actor, CancellationToken ct)
    {
        Require(IsIt(actor), 403, "Chỉ IT được xem người phụ trách.");
        return (await directory.GetActiveUsersAsync(actor.OrganizationId, ct)).Where(u => u.Role is Roles.Admin).OrderBy(u => u.DisplayName).Select(u => new SelfServiceTeamMemberDto(u.Id, u.DisplayName)).ToArray();
    }
    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(ActorContext actor, CancellationToken ct)
    {
        Human(actor);
        var receipts = await store.GetReceiptsAsync(actor.OrganizationId, actor.UserId, ct);
        return (await store.GetAnnouncementsAsync(actor.OrganizationId, ct)).Where(a => IsIt(actor) || a.StartsAt <= Now && a.EndsAt > Now).OrderByDescending(a => a.StartsAt)
            .Select(a => AnnouncementDto(a, receipts.SingleOrDefault(r => r.AnnouncementId == a.Id))).ToArray();
    }
    private static AnnouncementDto AnnouncementDto(SelfServiceAnnouncement announcement, SelfServiceAnnouncementReceipt? receipt) => new(announcement.Id, announcement.Title, announcement.Body, announcement.IsOutage, announcement.RequiresAcknowledgement,
        announcement.StartsAt, announcement.EndsAt, receipt?.AcknowledgedAt is not null, receipt?.AffectedAt is not null, announcement.CreatedAt);
    public Task<AnnouncementDto> CreateAnnouncementAsync(ActorContext actor, CreateAnnouncement input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Require(IsIt(actor), 403, "Chỉ IT được gửi thông báo.");
        Confirm(input.Reason, input.Confirmed);
        Require(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 200 && !string.IsNullOrWhiteSpace(input.Body) && input.Body.Length <= 4000 &&
            input.StartsAt < input.EndsAt && input.EndsAt > Now && input.StartsAt <= Now.AddDays(90) && input.EndsAt <= input.StartsAt.AddDays(90), 400, "Nội dung hoặc thời gian thông báo không hợp lệ.");
        var announcement = new SelfServiceAnnouncement
        {
            OrganizationId = actor.OrganizationId,
            Title = input.Title.Trim(),
            Body = input.Body.Trim(),
            IsOutage = input.IsOutage,
            RequiresAcknowledgement = input.RequiresAcknowledgement,
            StartsAt = input.StartsAt.ToUniversalTime(),
            EndsAt = input.EndsAt.ToUniversalTime(),
            CreatedAt = Now
        };
        store.Add(announcement);
        if (announcement.StartsAt <= Now) await PublishAnnouncementAsync(announcement, ct);
        audit.Record(actor.OrganizationId, actor.UserId, null, "CompanyAnnouncementCreated", input.Reason.Trim(), "Created");
        return AnnouncementDto(announcement, null);
    }, ct);
    public Task<AnnouncementDto> ReceiptAsync(ActorContext actor, Guid id, bool affected, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Human(actor);
        var announcement = (await store.GetAnnouncementsAsync(actor.OrganizationId, ct)).SingleOrDefault(a => a.Id == id && a.StartsAt <= Now && a.EndsAt > Now);
        Require(announcement is not null, 404, "Thông báo không còn có hiệu lực.");
        Require(!affected || announcement!.IsOutage, 400, "Chỉ sự cố chung mới có thể ghi nhận ảnh hưởng.");
        var receipt = (await store.GetReceiptsAsync(actor.OrganizationId, actor.UserId, ct)).SingleOrDefault(r => r.AnnouncementId == id);
        if (receipt is null)
        {
            receipt = new SelfServiceAnnouncementReceipt { OrganizationId = actor.OrganizationId, AnnouncementId = id, UserId = actor.UserId, CreatedAt = Now };
            store.Add(receipt);
        }
        if (affected) receipt.AffectedAt ??= Now; else receipt.AcknowledgedAt ??= Now;
        audit.Record(actor.OrganizationId, actor.UserId, null, affected ? "OutageAffectedReported" : "AnnouncementAcknowledged", "Nhân viên xác nhận thông báo", "Recorded");
        return AnnouncementDto(announcement!, receipt);
    }, ct);
    private async Task PublishAnnouncementAsync(SelfServiceAnnouncement announcement, CancellationToken ct)
    {
        foreach (var user in await directory.GetActiveUsersAsync(announcement.OrganizationId, ct))
            await NotifyAsync(announcement.OrganizationId, user.Id, announcement.Title, announcement.Body, null, $"announcement:{announcement.Id}", ct);
        announcement.PublishedAt = Now;
    }

    public async Task<IReadOnlyList<EmployeeNotificationDto>> GetNotificationsAsync(ActorContext actor, CancellationToken ct)
    {
        Human(actor);
        return (await store.GetNotificationsAsync(actor.OrganizationId, actor.UserId, ct)).OrderByDescending(n => n.CreatedAt).Take(200).Select(NotificationDto).ToArray();
    }
    private static EmployeeNotificationDto NotificationDto(SelfServiceNotification notification) => new(notification.Id, notification.Title, notification.Body, notification.RequestId, notification.ReadAt, notification.CreatedAt);
    public Task<EmployeeNotificationDto> ReadNotificationAsync(ActorContext actor, Guid id, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Human(actor);
        var notification = (await store.GetNotificationsAsync(actor.OrganizationId, actor.UserId, ct)).SingleOrDefault(n => n.Id == id);
        Require(notification is not null, 404, "Không tìm thấy thông báo.");
        notification!.ReadAt ??= Now;
        return NotificationDto(notification);
    }, ct);
    private async Task NotifyAsync(Guid organizationId, Guid userId, string title, string body, Guid? requestId, string key, CancellationToken ct)
    {
        if ((await store.GetNotificationsAsync(organizationId, userId, ct)).Any(n => n.DeduplicationKey == key)) return;
        var notification = new SelfServiceNotification { OrganizationId = organizationId, UserId = userId, Title = title, Body = body, RequestId = requestId, DeduplicationKey = key, CreatedAt = Now };
        store.Add(notification);
        foreach (var push in await store.GetPushDevicesAsync(organizationId, userId, ct))
            store.Add(new SelfServicePushDelivery { OrganizationId = organizationId, NotificationId = notification.Id, PushDeviceId = push.Id, NextAttemptAt = Now, CreatedAt = Now });
    }
    private async Task NotifyItAsync(Guid organizationId, string title, string body, Guid? requestId, string key, CancellationToken ct)
    {
        foreach (var user in (await directory.GetActiveUsersAsync(organizationId, ct)).Where(u => u.Role is Roles.Admin))
            await NotifyAsync(organizationId, user.Id, title, body, requestId, key, ct);
    }
    public Task<bool> RegisterPushAsync(ActorContext actor, RegisterPushDevice input, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        await ActorUserAsync(actor, ct);
        Require(input.Platform is "ios" or "android" && input.Token is { Length: <= 200 } && Regex.IsMatch(input.Token, "\\A(?:ExponentPushToken|ExpoPushToken)\\[[a-zA-Z0-9_-]+\\]\\z", RegexOptions.CultureInvariant), 400, "Mã nhận thông báo hoặc nền tảng không hợp lệ.");
        var existing = await store.FindPushDeviceAsync(input.Token!, ct);
        Require(existing is null || existing.OrganizationId == actor.OrganizationId && existing.UserId == actor.UserId, 409, "Thiết bị nhận thông báo đã được liên kết với tài khoản khác. Vui lòng đăng xuất tài khoản cũ.");
        if (existing is not null) { existing.Platform = input.Platform; existing.UpdatedAt = Now; return true; }
        Require((await store.GetPushDevicesAsync(actor.OrganizationId, actor.UserId, ct)).Count < 10, 400, "Tài khoản đã đăng ký quá nhiều thiết bị nhận thông báo.");
        store.Add(new SelfServicePushDevice { OrganizationId = actor.OrganizationId, UserId = actor.UserId, Token = input.Token!, Platform = input.Platform, CreatedAt = Now });
        return true;
    }, ct);
    public Task<bool> RemovePushAsync(ActorContext actor, string token, CancellationToken ct) => MutateAsync(actor.OrganizationId, async () =>
    {
        Human(actor);
        Require(!string.IsNullOrWhiteSpace(token) && token.Length <= 200, 400, "Mã nhận thông báo không hợp lệ.");
        var push = await store.FindPushDeviceAsync(token, ct);
        // Do not disclose another recipient's binding or allow deleting it.
        if (push is not null && push.OrganizationId == actor.OrganizationId && push.UserId == actor.UserId) store.RemovePushDevice(push);
        return true;
    }, ct);

    public static IReadOnlyList<HelpArticleDto> Help(ActorContext actor)
    {
        Human(actor);
        return
        [
            new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Network", "Không vào được mạng", ["Kiểm tra biểu tượng mạng ở góc màn hình.", "Kiểm tra dây mạng hoặc kết nối Wi-Fi của công ty.", "Nếu vẫn không được, bấm Báo sự cố để nhờ IT."]),
            new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Printer", "Không in được", ["Kiểm tra máy in đã bật và còn giấy.", "Chọn đúng máy in khi bấm In.", "Chụp ảnh thông báo lỗi và gửi yêu cầu cho IT."]),
            new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Slow", "Máy chạy chậm", ["Lưu công việc đang làm.", "Đóng các ứng dụng bạn không sử dụng.", "Nếu máy vẫn chậm, báo IT; đừng tự xóa tệp hệ thống."]),
            new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "Application", "Phần mềm không mở", ["Đọc và chụp ảnh thông báo lỗi nếu có.", "Thử mở lại phần mềm sau khi lưu công việc.", "Bấm Báo sự cố nếu vẫn chưa dùng được."]),
            new(Guid.Parse("10000000-0000-0000-0000-000000000005"), "Suspicious", "Có thông báo đáng ngờ", ["Dừng thao tác và không mở liên kết hoặc tệp lạ.", "Bấm Tôi nghi máy bị nhiễm virus để báo IT.", "Chờ IT hướng dẫn; hệ thống sẽ hiển thị rõ kết quả yêu cầu cô lập."]),
            new(Guid.Parse("10000000-0000-0000-0000-000000000006"), "Other", "Cần IT hỗ trợ", ["Chọn loại vấn đề gần nhất với tình trạng của bạn.", "Cho IT biết bạn có tiếp tục làm việc được không.", "Gửi ảnh lỗi sau khi kiểm tra và che thông tin riêng tư."])
        ];
    }
}
