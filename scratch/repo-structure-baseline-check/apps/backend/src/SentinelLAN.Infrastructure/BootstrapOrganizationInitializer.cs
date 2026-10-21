using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class BootstrapOrganizationInitializer
{
    public static async Task EnsureCreatedAsync(
        SentinelDbContext db,
        IPasswordHasher passwordHasher,
        string? organizationCode,
        string? organizationName,
        string? adminEmail,
        string? adminPassword,
        CancellationToken cancellationToken = default)
    {
        if (await db.Organizations.AnyAsync(cancellationToken))
        {
            var demo = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Code == "demo", cancellationToken);
            if (demo is not null)
            {
                var demoUsers = await db.Users.AsNoTracking()
                    .Where(x => x.OrganizationId == demo.Id)
                    .Select(x => x.PasswordHash)
                    .ToListAsync(cancellationToken);
                if (demoUsers.Any(hash => passwordHasher.Verify("local-demo-only", hash) != PasswordVerificationResult.Failed))
                    throw new InvalidOperationException("A development credential is present. Rotate it before starting outside Development.");
            }
            return;
        }

        var code = organizationCode?.Trim().ToLowerInvariant();
        var name = organizationName?.Trim();
        var email = adminEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code) || code.Length is < 3 or > 50 || !char.IsLetter(code[0]) ||
            !code.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new InvalidOperationException("SENTINELLAN_BOOTSTRAP_ORG_CODE must be 3-50 lowercase letters, digits or hyphens and start with a letter.");
        if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 100)
            throw new InvalidOperationException("SENTINELLAN_BOOTSTRAP_ORG_NAME must be 2-100 characters.");
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 ||
            !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new InvalidOperationException("SENTINELLAN_BOOTSTRAP_ADMIN_EMAIL must be a valid email address.");
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 16 ||
            new[] { "local-demo-only", "change-me", "replace-", "development-" }
                .Any(marker => adminPassword.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("SENTINELLAN_BOOTSTRAP_ADMIN_PASSWORD must be a unique secret of at least 16 characters.");

        var organization = new Organization { Code = code, Name = name };
        var admin = new User
        {
            OrganizationId = organization.Id,
            Email = email,
            DisplayName = "Organization Administrator",
            Role = Roles.Admin,
            PasswordHash = passwordHasher.Hash(adminPassword),
            Status = UserStatuses.Active
        };
        db.Add(organization);
        db.Add(admin);
        db.Add(new AuditLog
        {
            OrganizationId = organization.Id,
            ActorId = admin.Id,
            Action = "OrganizationBootstrapped",
            Reason = "Initial organization administrator created",
            Outcome = "Success"
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
