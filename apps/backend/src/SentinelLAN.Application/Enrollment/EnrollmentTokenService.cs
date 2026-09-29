using SentinelLAN.Domain;

namespace SentinelLAN.Application;

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
