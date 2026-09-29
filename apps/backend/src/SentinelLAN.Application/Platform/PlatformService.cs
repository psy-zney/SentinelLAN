using System.Net.Mail;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class PlatformService(IPlatformStore store, IPasswordHasher passwords, IActivationTokenGenerator tokens, ISecretHasher hashes, TimeProvider clock)
{
    public Task<IReadOnlyList<CompanySummary>> ListAsync(ActorContext actor, CancellationToken ct)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        return store.ListAsync(ct);
    }

    public Task<CompanyDetailView?> GetCompanyDetailsAsync(ActorContext actor, Guid id, CancellationToken ct)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        return store.GetCompanyDetailsAsync(id, ct);
    }

    public Task<PlatformSystemStatus> GetSystemStatusAsync(ActorContext actor, CancellationToken ct)
    {
        if (actor.Role != Roles.PlatformOwner) throw new UnauthorizedAccessException();
        return store.GetSystemStatusAsync(ct);
    }

    public async Task<(ManagementResultStatus Status, CompanyInvitation? Invitation)> CreateAsync(ActorContext actor, CreateCompanyRequest request, CancellationToken ct)
    {
        if (actor.Role != Roles.PlatformOwner) return (ManagementResultStatus.Forbidden, null);
        var now = clock.GetUtcNow();
        var code = request.Code?.Trim().ToLowerInvariant();
        var name = request.Name?.Trim();
        var email = request.AdminEmail?.Trim().ToLowerInvariant();
        var adminName = request.AdminName?.Trim();
        if (!Valid(request.Confirmation, now) || code is null || code.Length is < 3 or > 50 || code[0] is < 'a' or > 'z' ||
            !code.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') || name?.Length is not >= 2 or > 100 ||
            adminName?.Length is not >= 2 or > 100 || email is null || email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            return (ManagementResultStatus.Invalid, null);
        if (await store.ExistsAsync(code, name!, ct) || await store.NonceExistsAsync(actor.UserId, request.Confirmation.Nonce, ct))
            return (ManagementResultStatus.Conflict, null);

        var organization = new Organization { Code = code, Name = name! };
        var admin = new User
        {
            OrganizationId = organization.Id,
            Email = email,
            DisplayName = adminName!,
            Role = Roles.Admin,
            PasswordHash = passwords.Hash(tokens.GenerateToken()),
            Status = UserStatuses.PendingActivation
        };
        var raw = tokens.GenerateToken();
        var expiry = now.AddHours(24);
        store.AddCompany(organization, admin, new AccountActivationToken
        {
            OrganizationId = organization.Id,
            UserId = admin.Id,
            CreatedByUserId = actor.UserId,
            TokenHash = hashes.Create(raw),
            ExpiresAt = expiry
        });
        Record(actor, organization.Id, request.Confirmation, "CompanyCreated", now);
        if (!await store.SaveAsync(ct)) return (ManagementResultStatus.Conflict, null);
        return (ManagementResultStatus.Succeeded, new(organization.Id, code, email, $"/company/activate#token={Uri.EscapeDataString(raw)}", expiry));
    }

    public async Task<ManagementResultStatus> SetStatusAsync(ActorContext actor, Guid id, SetCompanyStatusRequest request, CancellationToken ct)
    {
        if (actor.Role != Roles.PlatformOwner) return ManagementResultStatus.Forbidden;
        var now = clock.GetUtcNow();
        if (!Valid(request.Confirmation, now)) return ManagementResultStatus.Invalid;
        var company = await store.FindAsync(id, ct);
        if (company is null || company.Code == PlatformIdentity.OrganizationCode) return ManagementResultStatus.NotFound;
        if (await store.NonceExistsAsync(actor.UserId, request.Confirmation.Nonce, ct)) return ManagementResultStatus.Conflict;
        company.IsSuspended = request.IsSuspended;
        company.UpdatedAt = now;
        if (request.IsSuspended) await store.RevokeCompanySessionsAsync(id, now, ct);
        Record(actor, id, request.Confirmation, request.IsSuspended ? "CompanySuspended" : "CompanyResumed", now);
        return await store.SaveAsync(ct) ? ManagementResultStatus.Succeeded : ManagementResultStatus.Conflict;
    }

    public async Task<(ManagementResultStatus Status, CompanyInvitation? Invitation)> ReissueAsync(ActorContext actor, Guid id, PlatformConfirmation confirmation, CancellationToken ct)
    {
        if (actor.Role != Roles.PlatformOwner) return (ManagementResultStatus.Forbidden, null);
        var now = clock.GetUtcNow();
        if (!Valid(confirmation, now)) return (ManagementResultStatus.Invalid, null);
        var company = await store.FindAsync(id, ct);
        if (company is null || company.Code == PlatformIdentity.OrganizationCode) return (ManagementResultStatus.NotFound, null);
        var admin = await store.FindPendingAdminAsync(id, ct);
        if (admin is null || company.IsSuspended || await store.NonceExistsAsync(actor.UserId, confirmation.Nonce, ct)) return (ManagementResultStatus.Conflict, null);
        var raw = tokens.GenerateToken();
        var expiry = now.AddHours(24);
        await store.ReplaceInvitationAsync(new AccountActivationToken
        {
            OrganizationId = id,
            UserId = admin.Id,
            CreatedByUserId = actor.UserId,
            TokenHash = hashes.Create(raw),
            ExpiresAt = expiry
        }, now, ct);
        Record(actor, id, confirmation, "CompanyAdminInvitationReissued", now);
        if (!await store.SaveAsync(ct)) return (ManagementResultStatus.Conflict, null);
        return (ManagementResultStatus.Succeeded, new(id, company.Code, admin.Email, $"/company/activate#token={Uri.EscapeDataString(raw)}", expiry));
    }

    private static bool Valid(PlatformConfirmation? confirmation, DateTimeOffset now) => confirmation is { Confirmed: true } &&
        confirmation.Reason?.Trim().Length is >= 3 and <= 1000 && confirmation.Nonce != Guid.Empty &&
        confirmation.ExpiresAt > now && confirmation.ExpiresAt <= now.AddMinutes(5);

    private void Record(ActorContext actor, Guid target, PlatformConfirmation confirmation, string action, DateTimeOffset now) =>
        store.AddOperation(new PlatformOperation { ActorId = actor.UserId, Nonce = confirmation.Nonce, ExpiresAt = confirmation.ExpiresAt },
            new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                Action = action,
                Reason = $"{confirmation.Reason.Trim()} (company {target})",
                Outcome = "Success",
                CreatedAt = now
            });
}
