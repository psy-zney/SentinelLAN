using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class PlatformOwnerInitializer
{
    public static async Task EnsureCreatedAsync(SentinelDbContext db, IPasswordHasher passwords, string? email, string? password, bool isDevelopment = false, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password))
        {
            if (!isDevelopment) return;
            email = "owner@sentinellan.local";
            password = "PlatformOwner#2026!SecureKey";
        }
        email = email?.Trim().ToLowerInvariant();
        if (email is null || email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email ||
            string.IsNullOrWhiteSpace(password) || password.Length is < 16 or > 1024 || (!isDevelopment && password.Contains("local-demo", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Set SENTINELLAN_PLATFORM_OWNER_EMAIL and a unique SENTINELLAN_PLATFORM_OWNER_PASSWORD of at least 16 characters.");

        if (await db.Users.AnyAsync(u => u.Email == email && u.Role == Roles.PlatformOwner, ct)) return;
        var organization = await db.Organizations.SingleOrDefaultAsync(o => o.Code == PlatformIdentity.OrganizationCode, ct);
        if (organization is null) { organization = new Organization { Code = PlatformIdentity.OrganizationCode, Name = "SentinelLAN Platform" }; db.Add(organization); }
        var owner = new User { OrganizationId = organization.Id, Email = email, DisplayName = "Chủ hệ thống", Role = Roles.PlatformOwner, PasswordHash = passwords.Hash(password) };
        db.Add(owner);
        db.Add(new AuditLog { OrganizationId = organization.Id, ActorId = owner.Id, Action = "PlatformOwnerBootstrapped", Reason = isDevelopment ? "Development platform owner provisioned" : "Initial platform owner provisioned from environment", Outcome = "Success" });
        await db.SaveChangesAsync(ct);
    }
}
