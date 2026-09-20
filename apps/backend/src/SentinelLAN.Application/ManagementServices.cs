using System.Net.Mail;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IManagementStore
{
    Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task<bool> HasActiveDeviceAssignmentAsync(Guid organizationId, Guid userId, Guid? excludingDeviceId, CancellationToken cancellationToken);
    Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<DeviceCredential?> FindCredentialAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddEnrollmentToken(DeviceEnrollmentToken token);
    void AddAudit(AuditLog auditLog);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}

public interface IEnrollmentSecretGenerator
{
    string GenerateToken();
}

public sealed class UserManagementService(IManagementStore store)
{
    public async Task<(ManagementResultStatus Status, UserSummaryDto? User)> CreateAsync(
        ActorContext actor, CreateUserRequest request, IPasswordHasher passwordHasher, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin || !ManagementValidation.IsValid(request))
            return (actor.Role == Roles.Admin ? ManagementResultStatus.Invalid : ManagementResultStatus.Forbidden, null);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await store.FindUserByEmailAsync(actor.OrganizationId, email, cancellationToken) is not null)
            return (ManagementResultStatus.Conflict, null);

        var user = new User
        {
            OrganizationId = actor.OrganizationId,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PasswordHash = passwordHasher.Hash(request.Password)
        };
        store.AddUser(user);
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            Action = "UserCreated",
            Reason = $"{request.Reason.Trim()} (target user {user.Id})",
            Outcome = "Success"
        });
        if (!await store.TrySaveChangesAsync(cancellationToken)) return (ManagementResultStatus.Conflict, null);
        return (ManagementResultStatus.Succeeded, new UserSummaryDto(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt));
    }
}

public sealed class EnrollmentTokenService(IManagementStore store, IEnrollmentSecretGenerator secretGenerator, ISecretHasher secretHasher)
{
    public async Task<(ManagementResultStatus Status, EnrollmentTokenResponse? Token)> CreateAsync(
        ActorContext actor, EnrollmentTokenRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin || !ManagementValidation.IsValid(request))
            return (actor.Role == Roles.Admin ? ManagementResultStatus.Invalid : ManagementResultStatus.Forbidden, null);

        var rawToken = secretGenerator.GenerateToken();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(request.ValidForMinutes);
        var enrollmentToken = new DeviceEnrollmentToken
        {
            OrganizationId = actor.OrganizationId,
            TokenHash = secretHasher.Create(rawToken),
            ExpiresAt = expiresAt
        };
        store.AddEnrollmentToken(enrollmentToken);
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            Action = "EnrollmentTokenCreated",
            Reason = $"{request.Reason.Trim()} (token {enrollmentToken.Id}, expires {expiresAt:O})",
            Outcome = "Success"
        });
        if (!await store.TrySaveChangesAsync(cancellationToken)) return (ManagementResultStatus.Conflict, null);
        return (ManagementResultStatus.Succeeded, new EnrollmentTokenResponse(rawToken, expiresAt));
    }
}

public interface ISecretHasher
{
    string Create(string value);
}

public sealed class DeviceManagementService(IManagementStore store)
{
    public async Task<DeviceManagementOutcome> AssignAsync(
        ActorContext actor, Guid deviceId, DeviceAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin) return new(null, ManagementResultStatus.Forbidden);
        if (!ManagementValidation.IsValid(request)) return new(null, ManagementResultStatus.Invalid);

        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return new(null, ManagementResultStatus.NotFound);
        if (device.IsRevoked) return new(null, ManagementResultStatus.Conflict);

        if (request.AssignedUserId is Guid assignedUserId)
        {
            var user = await store.FindUserAsync(actor.OrganizationId, assignedUserId, cancellationToken);
            if (user is null) return new(null, ManagementResultStatus.NotFound);
            if (user.Role != Roles.Employee) return new(null, ManagementResultStatus.Invalid);
            if (await store.HasActiveDeviceAssignmentAsync(actor.OrganizationId, assignedUserId, deviceId, cancellationToken))
                return new(null, ManagementResultStatus.Conflict);
        }

        device.AssignedUserId = request.AssignedUserId;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = request.AssignedUserId.HasValue ? "DeviceAssigned" : "DeviceUnassigned",
            Reason = request.Reason.Trim(),
            Outcome = "Success"
        });
        if (!await store.TrySaveChangesAsync(cancellationToken)) return new(null, ManagementResultStatus.Conflict);
        return new(ToDto(device), ManagementResultStatus.Succeeded);
    }

    public async Task<DeviceManagementOutcome> RevokeAsync(
        ActorContext actor, Guid deviceId, RevokeDeviceRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin) return new(null, ManagementResultStatus.Forbidden);
        if (!ManagementValidation.IsValid(request)) return new(null, ManagementResultStatus.Invalid);

        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return new(null, ManagementResultStatus.NotFound);
        if (!device.IsRevoked)
        {
            device.IsRevoked = true;
            device.AssignedUserId = null;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            var credential = await store.FindCredentialAsync(actor.OrganizationId, deviceId, cancellationToken);
            if (credential is not null) credential.RevokedAt ??= DateTimeOffset.UtcNow;
            store.AddAudit(new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                DeviceId = device.Id,
                Action = "DeviceRevoked",
                Reason = request.Reason.Trim(),
                Outcome = "Success"
            });
            if (!await store.TrySaveChangesAsync(cancellationToken)) return new(null, ManagementResultStatus.Conflict);
        }
        return new(ToDto(device), ManagementResultStatus.Succeeded);
    }

    private static DeviceDto ToDto(Device device) =>
        new(device.Id, device.Name, device.OsVersion, device.AgentVersion, device.LastSeenAt,
            device.IsOnline(DateTimeOffset.UtcNow), device.AssignedUserId, device.IsRevoked);
}

public sealed record DeviceManagementOutcome(DeviceDto? Device, ManagementResultStatus Status);
public sealed record RevokeDeviceRequest(string Reason, bool Confirmed);

public static class ManagementValidation
{
    public static bool IsValid(CreateUserRequest request) =>
        IsReasonConfirmed(request.Reason, request.Confirmed) &&
        IsEmail(request.Email) && !string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length is >= 2 and <= 100 &&
        !string.IsNullOrEmpty(request.Password) && request.Password.Length is >= 12 and <= 128 &&
        request.Role is Roles.Admin or Roles.Technician or Roles.Employee;

    public static bool IsValid(EnrollmentTokenRequest request) =>
        IsReasonConfirmed(request.Reason, request.Confirmed) && request.ValidForMinutes is >= 1 and <= 60;

    public static bool IsValid(DeviceAssignmentRequest request) => IsReasonConfirmed(request.Reason, request.Confirmed);
    public static bool IsValid(RevokeDeviceRequest request) => IsReasonConfirmed(request.Reason, request.Confirmed);

    private static bool IsReasonConfirmed(string? reason, bool confirmed) => confirmed && reason?.Trim().Length is >= 3 and <= 1000;

    private static bool IsEmail(string? value)
    {
        if (value is null || value.Trim().Length is < 3 or > 254) return false;
        try { return new MailAddress(value.Trim()).Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }
}
