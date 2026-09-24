using System.Net.Mail;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IManagementStore
{
    Task<User?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
    Task<User?> FindUserByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetUsersAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<User>>([]);
    Task<int> CountActiveAdminsAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult(1);
    Task<IReadOnlyList<RefreshSession>> GetActiveUserSessionsAsync(Guid organizationId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RefreshSession>>([]);
    Task<bool> HasActiveDeviceAssignmentAsync(Guid organizationId, Guid userId, Guid? excludingDeviceId, CancellationToken cancellationToken);
    Task<Device?> FindDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    Task<DeviceCredential?> FindCredentialAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddEnrollmentToken(DeviceEnrollmentToken token);
    void AddAudit(AuditLog auditLog);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    // Activation Tokens
    void AddActivationToken(AccountActivationToken token) { }
    Task<AccountActivationToken?> FindActiveActivationTokenByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<AccountActivationToken?>(null);
    Task<AccountActivationToken?> FindActivationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<AccountActivationToken?>(null);
    Task<IReadOnlyList<AccountActivationToken>> GetActiveActivationTokensByUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AccountActivationToken>>([]);

    // QR Labels
    void AddQrLabel(DeviceQrLabel label) { }
    Task<DeviceQrLabel?> FindActiveQrLabelByDeviceAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<DeviceQrLabel?>(null);
    Task<DeviceQrLabel?> FindQrLabelByHashAsync(string codeHash, CancellationToken cancellationToken) => Task.FromResult<DeviceQrLabel?>(null);
    Task<DeviceQrLabel?> FindTenantQrLabelByHashAsync(Guid organizationId, string codeHash, CancellationToken cancellationToken) => Task.FromResult<DeviceQrLabel?>(null);
}

public interface IEnrollmentSecretGenerator
{
    string GenerateToken();
}

public interface IActivationTokenGenerator
{
    string GenerateToken();
}

public interface IQrCodeGenerator
{
    string GenerateOpaqueCode();
}

internal sealed class DefaultActivationTokenGenerator : IActivationTokenGenerator
{
    public string GenerateToken() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

internal sealed class DefaultSecretHasher : ISecretHasher
{
    public string Create(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}

internal sealed class DefaultQrCodeGenerator : IQrCodeGenerator
{
    public string GenerateOpaqueCode() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public sealed record CreateUserOutcome(
    ManagementResultStatus Status,
    UserSummaryDto? User,
    string? ActivationToken = null,
    string? ActivationUrl = null,
    DateTimeOffset? ExpiresAt = null)
{
    public void Deconstruct(out ManagementResultStatus status, out UserSummaryDto? user)
    {
        status = Status;
        user = User;
    }
}

public sealed class UserManagementService(
    IManagementStore store,
    IActivationTokenGenerator? tokenGenerator = null,
    ISecretHasher? secretHasher = null)
{
    private readonly IActivationTokenGenerator tokenGenerator = tokenGenerator ?? new DefaultActivationTokenGenerator();
    private readonly ISecretHasher secretHasher = secretHasher ?? new DefaultSecretHasher();

    public async Task<(ManagementResultStatus Status, UserSummaryDto? User)> SetStatusAsync(
        ActorContext actor, Guid userId, SetUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin) return (ManagementResultStatus.Forbidden, null);
        if (!request.Confirmed || request.Reason?.Trim().Length is not >= 3 or > 1000 ||
            request.Status is not (UserStatuses.Active or UserStatuses.Locked))
            return (ManagementResultStatus.Invalid, null);

        var user = await store.FindUserAsync(actor.OrganizationId, userId, cancellationToken);
        if (user is null) return (ManagementResultStatus.NotFound, null);
        if (user.Status == UserStatuses.PendingActivation || userId == actor.UserId)
            return (ManagementResultStatus.Conflict, null);
        if (user.Status != request.Status)
        {
            if (user.Role == Roles.Admin && request.Status == UserStatuses.Locked &&
                await store.CountActiveAdminsAsync(actor.OrganizationId, cancellationToken) <= 1)
                return (ManagementResultStatus.Conflict, null);

            user.Status = request.Status;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = DateTimeOffset.UtcNow;
            if (request.Status == UserStatuses.Locked)
            {
                var sessions = await store.GetActiveUserSessionsAsync(actor.OrganizationId, userId, DateTimeOffset.UtcNow, cancellationToken);
                foreach (var session in sessions)
                    session.TryRevoke(DateTimeOffset.UtcNow, "Account locked by administrator");
            }
            store.AddAudit(new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                Action = request.Status == UserStatuses.Locked ? "UserLocked" : "UserUnlocked",
                Reason = $"{request.Reason.Trim()} (target user {user.Id})",
                Outcome = "Success"
            });
            if (!await store.TrySaveChangesAsync(cancellationToken)) return (ManagementResultStatus.Conflict, null);
        }
        return (ManagementResultStatus.Succeeded,
            new UserSummaryDto(user.Id, user.Email, user.DisplayName, user.Role, user.Status, user.CreatedAt));
    }

    public async Task<CreateUserOutcome> CreateAsync(
        ActorContext actor, CreateUserRequest request, IPasswordHasher passwordHasher, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin || !ManagementValidation.IsValid(request))
            return new(actor.Role == Roles.Admin ? ManagementResultStatus.Invalid : ManagementResultStatus.Forbidden, null);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await store.FindUserByEmailAsync(actor.OrganizationId, email, cancellationToken) is not null)
            return new(ManagementResultStatus.Conflict, null);

        var isPending = string.IsNullOrWhiteSpace(request.Password);
        var passwordHash = isPending ? passwordHasher.Hash(Guid.NewGuid().ToString("N")) : passwordHasher.Hash(request.Password!);
        var user = new User
        {
            OrganizationId = actor.OrganizationId,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PasswordHash = passwordHash,
            Status = isPending ? UserStatuses.PendingActivation : UserStatuses.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        store.AddUser(user);

        string? rawToken = null;
        string? activationUrl = null;
        DateTimeOffset? expiresAt = null;

        if (isPending)
        {
            rawToken = tokenGenerator.GenerateToken();
            var tokenHash = secretHasher.Create(rawToken);
            expiresAt = DateTimeOffset.UtcNow.AddHours(Math.Clamp(request.ValidForHours, 1, 168));
            var activationToken = new AccountActivationToken
            {
                OrganizationId = actor.OrganizationId,
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt.Value,
                CreatedByUserId = actor.UserId
            };
            store.AddActivationToken(activationToken);
            activationUrl = $"/activate?token={rawToken}";

            store.AddAudit(new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                Action = "UserCreatedWithInvitation",
                Reason = $"{request.Reason.Trim()} (target user {user.Id}, invitation expires {expiresAt:O})",
                Outcome = "Success"
            });
        }
        else
        {
            store.AddAudit(new AuditLog
            {
                OrganizationId = actor.OrganizationId,
                ActorId = actor.UserId,
                Action = "UserCreated",
                Reason = $"{request.Reason.Trim()} (target user {user.Id})",
                Outcome = "Success"
            });
        }

        if (!await store.TrySaveChangesAsync(cancellationToken)) return new(ManagementResultStatus.Conflict, null);
        var summary = new UserSummaryDto(user.Id, user.Email, user.DisplayName, user.Role, user.Status, user.CreatedAt, isPending);
        return new(ManagementResultStatus.Succeeded, summary, rawToken, activationUrl, expiresAt);
    }

    public async Task<(ManagementResultStatus Status, ReissueActivationTokenResponse? Response, string Message)> ReissueActivationTokenAsync(
        ActorContext actor, Guid userId, ReissueActivationTokenRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin)
            return (ManagementResultStatus.Forbidden, null, "Only administrators can reissue activation tokens.");

        if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 1000)
            return (ManagementResultStatus.Invalid, null, "Reason (3-1000 chars) and confirmation are required.");

        var user = await store.FindUserAsync(actor.OrganizationId, userId, cancellationToken);
        if (user is null)
            return (ManagementResultStatus.NotFound, null, "User not found.");

        if (user.Status == UserStatuses.Locked)
            return (ManagementResultStatus.Invalid, null, "Locked users cannot be issued activation tokens.");

        var existingTokens = await store.GetActiveActivationTokensByUserAsync(actor.OrganizationId, userId, cancellationToken);
        foreach (var t in existingTokens) t.Revoke(DateTimeOffset.UtcNow);

        var rawToken = tokenGenerator.GenerateToken();
        var tokenHash = secretHasher.Create(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(Math.Clamp(request.ValidForHours, 1, 168));
        var newToken = new AccountActivationToken
        {
            OrganizationId = actor.OrganizationId,
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByUserId = actor.UserId
        };
        store.AddActivationToken(newToken);

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            Action = "ActivationTokenReissued",
            Reason = $"{request.Reason.Trim()} (target user {userId}, expires {expiresAt:O})",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken))
            return (ManagementResultStatus.Conflict, null, "Could not reissue token due to concurrency conflict.");

        return (ManagementResultStatus.Succeeded, new ReissueActivationTokenResponse(rawToken, $"/activate?token={rawToken}", expiresAt), "Token reissued successfully.");
    }

    public async Task<(ManagementResultStatus Status, string Message)> RevokeActivationTokenAsync(
        ActorContext actor, Guid userId, RevokeActivationTokenRequest request, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin)
            return (ManagementResultStatus.Forbidden, "Only administrators can revoke activation tokens.");

        if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 1000)
            return (ManagementResultStatus.Invalid, "Reason and confirmation are required.");

        var user = await store.FindUserAsync(actor.OrganizationId, userId, cancellationToken);
        if (user is null)
            return (ManagementResultStatus.NotFound, "User not found.");

        var existingTokens = await store.GetActiveActivationTokensByUserAsync(actor.OrganizationId, userId, cancellationToken);
        foreach (var t in existingTokens) t.Revoke(DateTimeOffset.UtcNow);

        store.AddAudit(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            Action = "ActivationTokenRevoked",
            Reason = $"{request.Reason.Trim()} (target user {userId})",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken))
            return (ManagementResultStatus.Conflict, "Could not revoke token due to concurrency conflict.");

        return (ManagementResultStatus.Succeeded, "Active activation tokens revoked.");
    }

    public async Task<ValidateActivationTokenResponse> ValidateActivationTokenAsync(
        string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return new ValidateActivationTokenResponse(false, "Token is required.");

        var tokenHash = secretHasher.Create(rawToken.Trim());
        var token = await store.FindActivationTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null || !token.IsActive(DateTimeOffset.UtcNow))
            return new ValidateActivationTokenResponse(false, "Token is invalid, expired, or already used.");

        var user = await store.FindUserByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status == UserStatuses.Locked)
            return new ValidateActivationTokenResponse(false, "Token is invalid.");

        return new ValidateActivationTokenResponse(true, "Token is valid.");
    }

    public async Task<(ManagementResultStatus Status, string Message)> ActivateAccountAsync(
        ActivateAccountRequest request, IPasswordHasher passwordHasher, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return (ManagementResultStatus.Invalid, "Token is required.");

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 12 || request.Password.Length > 1024)
            return (ManagementResultStatus.Invalid, "Password must contain between 12 and 1024 characters.");

        var tokenHash = secretHasher.Create(request.Token.Trim());
        var token = await store.FindActivationTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null || !token.TryUse(DateTimeOffset.UtcNow))
            return (ManagementResultStatus.Invalid, "Activation token is invalid, expired, or has already been used.");

        var user = await store.FindUserByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status == UserStatuses.Locked)
            return (ManagementResultStatus.Invalid, "Account cannot be activated.");

        user.Status = UserStatuses.Active;
        user.PasswordHash = passwordHasher.Hash(request.Password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var otherTokens = await store.GetActiveActivationTokensByUserAsync(user.OrganizationId, user.Id, cancellationToken);
        foreach (var ot in otherTokens)
        {
            if (ot.Id != token.Id) ot.Revoke(DateTimeOffset.UtcNow);
        }

        store.AddAudit(new AuditLog
        {
            OrganizationId = user.OrganizationId,
            ActorId = user.Id,
            Action = "AccountActivated",
            Reason = "User activated account with one-time activation token",
            Outcome = "Success"
        });

        if (!await store.TrySaveChangesAsync(cancellationToken))
            return (ManagementResultStatus.Conflict, "Could not activate account due to concurrent modification.");

        return (ManagementResultStatus.Succeeded, "Account activated successfully.");
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        if (actor.Role != Roles.Admin) return [];
        var users = await store.GetUsersAsync(actor.OrganizationId, cancellationToken);
        var result = new List<UserSummaryDto>(users.Count);
        foreach (var u in users)
        {
            var activeTokens = await store.GetActiveActivationTokensByUserAsync(actor.OrganizationId, u.Id, cancellationToken);
            result.Add(new UserSummaryDto(u.Id, u.Email, u.DisplayName, u.Role, u.Status, u.CreatedAt, activeTokens.Count > 0));
        }
        return result;
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
        (string.IsNullOrEmpty(request.Password) || (request.Password.Length is >= 12 and <= 1024)) &&
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
