using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

// Infrastructure adapters implement the cross-module Application ports. The
// self-service store never reads another module's tables.
public sealed class SelfServiceDirectory(SentinelDbContext db) : ISelfServiceDirectory
{
    public Task<Device?> FindAssignedDeviceAsync(Guid org, Guid user, CancellationToken ct) => db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.AssignedUserId == user && !x.IsRevoked, ct);
    public Task<Device?> FindDeviceAsync(Guid org, Guid id, CancellationToken ct) => db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
    public Task<User?> FindUserAsync(Guid org, Guid id, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
    public async Task<IReadOnlyList<User>> GetActiveUsersAsync(Guid org, CancellationToken ct) => await db.Users.AsNoTracking().Where(x => x.OrganizationId == org && x.Status == UserStatuses.Active).ToListAsync(ct);
    public async Task<IReadOnlyList<Device>> GetDevicesAsync(Guid org, CancellationToken ct) => await db.Devices.AsNoTracking().Where(x => x.OrganizationId == org).ToListAsync(ct);
    public async Task<IReadOnlyList<Guid>> GetOrganizationIdsAsync(CancellationToken ct) => await db.Organizations.Select(x => x.Id).ToListAsync(ct);
}

public sealed class SelfServiceCommands(SentinelDbContext db, ICommandSigner signer) : ISelfServiceCommands
{
    public DeviceCommand Queue(Guid org, Guid device, Guid actor, string type, string reason, string parameter, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (type is not ("InstallApprovedApp" or "PauseAgent" or "UninstallAgent" or "IsolateNetwork")) throw new InvalidOperationException("Unsupported self-service command.");
        var command = new DeviceCommand
        {
            OrganizationId = org,
            DeviceId = device,
            IssuedByUserId = actor,
            Type = type,
            Reason = reason,
            Parameter = parameter,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Signature = "pending",
            IssuedAt = now,
            ExpiresAt = expiresAt,
            Status = DeviceCommandStatus.Pending
        };
        command.Signature = signer.Sign(command);
        db.Commands.Add(command);
        db.AuditLogs.Add(new AuditLog
        {
            OrganizationId = org,
            ActorId = actor,
            DeviceId = device,
            Action = $"CommandCreated:{type}",
            Reason = reason,
            Outcome = "Pending"
        });
        return command;
    }
    public async Task<(DeviceCommand? Command, CommandResult? Result)> GetAsync(Guid org, Guid id, CancellationToken ct)
    {
        var command = db.Commands.Local.SingleOrDefault(x => x.OrganizationId == org && x.Id == id)
            ?? await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
        var result = await db.CommandResults.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.CommandId == id, ct);
        return (command, result);
    }
}

public sealed class SelfServiceAudit(SentinelDbContext db) : ISelfServiceAudit
{
    public void Record(Guid org, Guid actor, Guid? device, string action, string reason, string outcome) =>
        db.AuditLogs.Add(new AuditLog { OrganizationId = org, ActorId = actor, DeviceId = device, Action = action, Reason = reason, Outcome = outcome });
    public void CriticalAlert(Guid org, Guid device, string message) => db.Alerts.Add(new Alert { OrganizationId = org, DeviceId = device, Severity = "Critical", Message = message });
}

public sealed class MaintenanceCodeProtector(string key) : IMaintenanceCodeProtector
{
    // A server-held HMAC pepper prevents an offline eight-digit search from a
    // database-only disclosure. The code is bound to the request, tenant, user,
    // device and action, and plaintext is never persisted.
    public string Generate() => RandomNumberGenerator.GetInt32(100_000_000).ToString("D8", CultureInfo.InvariantCulture);
    public string Protect(SelfServiceRequest request, string code) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),
        Encoding.UTF8.GetBytes($"{request.OrganizationId:D}|{request.Id:D}|{request.UserId:D}|{request.DeviceId:D}|{request.Kind}|{code}")));
    public bool Matches(SelfServiceRequest request, string code)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Protect(request, code)), Convert.FromHexString(request.MaintenanceCodeHash!)); }
        catch (FormatException) { return false; }
    }
}
