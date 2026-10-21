using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record EnrollmentBatchRequest(int Count, string Platform, AdministrativeConfirmation Confirmation);
public sealed class EnrollmentBatchService(IManagementStore store, IEnrollmentSecretGenerator generator, ISecretHasher hasher, IAdministrativeRequestJournal journal, TimeProvider clock)
{
    public async Task<IReadOnlyList<EnrollmentTokenResponse>> CreateAsync(ActorContext actor, EnrollmentBatchRequest request, CancellationToken ct)
    {
        if (actor.Role != Roles.Admin) throw new OrganizationConfigurationException(403, "Chỉ Admin được cấp mã đăng ký.");
        var now = clock.GetUtcNow(); var input = request.Confirmation;
        if (request.Count is < 1 or > 50 || request.Platform is not ("Windows" or "Linux") || input is null || !input.Confirmed || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 3 or > 1000 ||
            input.Nonce == Guid.Empty || input.ExpiresAt <= now || input.ExpiresAt > now.AddMinutes(5)) throw new OrganizationConfigurationException(400, "Cần chọn 1–50 máy, nền tảng, lý do và xác nhận còn hạn.");
        if (await journal.NonceExistsAsync(actor.OrganizationId, input.Nonce, ct)) throw new OrganizationConfigurationException(409, "Lô mã đã được cấp. Hãy cấp lô mới nếu đã mất bản tải.");
        var result = new List<EnrollmentTokenResponse>(); var unique = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < request.Count; i++)
        {
            var token = generator.GenerateToken();
            if (!unique.Add(token)) throw new InvalidOperationException("Enrollment generator produced a duplicate secret.");
            store.AddEnrollmentToken(new DeviceEnrollmentToken { OrganizationId = actor.OrganizationId, TokenHash = hasher.Create(token), ExpiresAt = now.AddMinutes(15), CreatedAt = now });
            result.Add(new(token, now.AddMinutes(15)));
        }
        journal.Reserve(new AdministrativeRequestNonce { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, Nonce = input.Nonce, Operation = "EnrollmentBatchCreated", ExpiresAt = input.ExpiresAt });
        journal.Audit(new AuditLog { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, Action = "EnrollmentBatchCreated", Reason = input.Reason.Trim(), Outcome = $"{request.Count} {request.Platform};15m" });
        if (!await journal.TrySaveAsync(ct)) throw new OrganizationConfigurationException(409, "Có thao tác đồng thời; lô mã chưa được cấp.");
        return result;
    }
}
