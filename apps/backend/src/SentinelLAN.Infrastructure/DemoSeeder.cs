using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class DemoSeeder
{
    public static async Task SeedAsync(SentinelDbContext db, IPasswordHasher passwordHasher, Func<string, string?> getSetting, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational()) await db.Database.MigrateAsync(cancellationToken);
        else await db.Database.EnsureCreatedAsync(cancellationToken);
        if (await db.Organizations.AnyAsync(cancellationToken)) return;
        var organization = new Organization { Code = "demo", Name = "SentinelLAN Demo" };
        var password = getSetting("SENTINELLAN_DEMO_ADMIN_PASSWORD") ?? "local-demo-only";
        var admin = new User { OrganizationId = organization.Id, Email = getSetting("SENTINELLAN_DEMO_ADMIN_EMAIL") ?? "admin@sentinellan.local", DisplayName = "Demo Admin", Role = Roles.Admin, PasswordHash = passwordHasher.Hash(password) };
        var technician = new User { OrganizationId = organization.Id, Email = "technician@sentinellan.local", DisplayName = "Demo Technician", Role = Roles.Technician, PasswordHash = passwordHasher.Hash(password) };
        var employee = new User { OrganizationId = organization.Id, Email = "employee@sentinellan.local", DisplayName = "Demo Employee", Role = Roles.Employee, PasswordHash = passwordHasher.Hash(password) };
        var employeeDevice = new Device
        {
            OrganizationId = organization.Id,
            AssignedUserId = employee.Id,
            Name = "EMPLOYEE-DEMO-PC",
            OsVersion = "Windows 11 24H2",
            AgentVersion = "0.1.0",
            LastSeenAt = DateTimeOffset.UtcNow
        };
        var standardPolicy = new Policy { OrganizationId = organization.Id, Name = "Standard", IdleTimeoutMinutes = 15, UsbMode = "ReadOnly" };
        db.AddRange(
            organization,
            admin,
            technician,
            employee,
            employeeDevice,
            standardPolicy,
            new PolicyAssignment { OrganizationId = organization.Id, PolicyId = standardPolicy.Id, DeviceId = employeeDevice.Id });
        var token = getSetting("SENTINELLAN_ENROLLMENT_TOKEN") ?? "local-enroll-only";
        db.Add(new DeviceEnrollmentToken { OrganizationId = organization.Id, TokenHash = SecretHash.Create(token), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        await db.SaveChangesAsync(cancellationToken);
    }
}
