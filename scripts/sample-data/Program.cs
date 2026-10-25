using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.SampleData;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task Main(string[] args)
    {
        const string dataset = "office-sample-v1";
        var mode = args.FirstOrDefault() ?? "validate";
        if (mode is not ("validate" or "seed" or "verify")) throw new InvalidOperationException("Use validate, seed or verify.");
        var input = JsonSerializer.Deserialize<SampleInput>(File.ReadAllText(args.ElementAtOrDefault(1) ?? "company-sample.json"),
            JsonOptions) ?? throw new InvalidOperationException("Dataset is missing.");
        if (input.Devices.Length != 12 || input.Issues.Length != 12 || input.Apps.Length != 2 || input.Alerts.Length != 6 ||
            input.Devices.Select(x => x.Code).Distinct().Count() != 12 ||
            input.Issues.Any(x => x.Device < 0 || x.Device >= input.Devices.Length ||
                x.Status is not ("Open" or "InProgress" or "AwaitingEmployee" or "Resolved" or "Closed")) ||
            input.Apps.Any(x => !Uri.TryCreate(x.PackageUrl, UriKind.Absolute, out var url) ||
                url.Scheme != "https" || url.Host != "manager.zney295.id.vn" ||
                !url.AbsolutePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                x.Sha256.Length != 64 || !x.Sha256.All(Uri.IsHexDigit) ||
                x.PublisherThumbprint.Length != 40 || !x.PublisherThumbprint.All(Uri.IsHexDigit)))
            throw new InvalidOperationException("Invalid sample dataset.");
        if (mode == "validate")
        {
            Console.WriteLine("VALIDATED: 12 synthetic employees/devices, 12 support requests and 2 verified package entries.");
            return;
        }

        var orgId = Guid.Parse(Required("SAMPLE_ORGANIZATION_ID"));
        var adminId = Guid.Parse(Required("SAMPLE_ADMIN_ID"));
        var options = new DbContextOptionsBuilder<SentinelDbContext>()
            .UseNpgsql(Required("ConnectionStrings__SentinelLAN")).Options;
        await using var db = new SentinelDbContext(options, new SampleTenant(orgId),
            new SensitiveDataCipher(Required("SENTINELLAN_DATA_ENCRYPTION_KEY")));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = timeout.Token;
        var company = await db.Organizations.SingleAsync(x => x.Id == orgId, ct);
        if (company.Code != "zney295" || company.IsSuspended) throw new InvalidOperationException("Unexpected company.");
        var admin = await db.Users.SingleAsync(x => x.Id == adminId && x.OrganizationId == orgId, ct);
        if (admin.Email != "lequangkhanh295@gmail.com" || admin.Role != "Admin" || admin.Status != "Active")
            throw new InvalidOperationException("Unexpected administrator.");
        var markerId = Id("audit");
        if (mode == "verify" || await db.AuditLogs.AnyAsync(x => x.Id == markerId && x.OrganizationId == orgId, ct))
        {
            await Verify();
            return;
        }
        if (Required("SAMPLE_CONFIRMED") != "true" ||
            !DateTimeOffset.TryParse(Required("SAMPLE_EXPIRES_AT"), out var expiry) ||
            expiry < DateTimeOffset.UtcNow || expiry > DateTimeOffset.UtcNow.AddMinutes(10) ||
            !Guid.TryParse(Required("SAMPLE_NONCE"), out var nonce))
            throw new InvalidOperationException("Explicit confirmation, nonce and a short expiry are required.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({783921564L})", ct);
        if (await db.AuditLogs.AnyAsync(x => x.Id == markerId && x.OrganizationId == orgId, ct))
        {
            await transaction.RollbackAsync(ct);
            await Verify();
            return;
        }
        var now = DateTimeOffset.UtcNow;
        var historical = now.AddDays(-3);
        var hasher = new Pbkdf2PasswordHasher();
        var departments = input.Devices.Select(x => x.Department).Distinct().ToArray();
        foreach (var department in departments)
            db.Departments.Add(new Department { Id = Id("department:" + department), OrganizationId = orgId, Name = "[MẪU] " + department });
        var policy = new Policy { Id = Id("policy"), OrganizationId = orgId, Name = "[MẪU] Cấu hình máy văn phòng — chưa áp dụng", IdleTimeoutMinutes = 15, UsbMode = "ReadOnly" };
        db.Policies.Add(policy);
        var staff = new List<User>();
        var machines = new List<Device>();
        for (var i = 0; i < input.Devices.Length; i++)
        {
            var item = input.Devices[i];
            var person = new User
            {
                Id = Id("user:" + i),
                OrganizationId = orgId,
                DisplayName = "[MẪU] " + item.Employee,
                Email = $"sample.{item.Code.ToLowerInvariant()}@example.invalid",
                Role = "Employee",
                Status = "Locked",
                PasswordHash = hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
                CreatedAt = historical.AddDays(-60)
            };
            var device = new Device
            {
                Id = Id("device:" + i),
                OrganizationId = orgId,
                Name = "[MẪU] " + item.Code,
                AssignedUserId = person.Id,
                OsVersion = item.Os,
                AgentVersion = "sample-only",
                LastSeenAt = null,
                IsRevoked = false,
                SerialNumber = "SAMPLE-" + item.Code,
                Manufacturer = item.Manufacturer,
                Model = item.Model,
                AssetType = item.Type,
                LocationCampus = "Dữ liệu giả lập",
                LocationBuilding = "Văn phòng mẫu",
                LocationFloor = item.Floor,
                LocationRoom = item.Department,
                AssetStatus = "Active",
                PurchaseDate = historical.AddMonths(-12),
                PurchaseCost = item.Cost,
                WarrantyExpiresAt = historical.AddMonths(12),
                VendorName = "[MẪU] Nhà cung cấp thiết bị",
                SpecificationsJson = JsonSerializer.Serialize(new
                {
                    cpu = item.Cpu,
                    ram = item.Ram,
                    storage = item.Storage,
                    sampleData = true,
                    dataset,
                    note = "Dữ liệu dựng mẫu, chưa có Agent kết nối; phần mềm dưới đây không phải kiểm kê thực tế.",
                    sampleInstalledApplications = item.Applications
                }),
                CreatedAt = historical.AddDays(-60)
            };
            // Newer versions associate department membership explicitly; older deployed versions use the room field.
            typeof(User).GetProperty("DepartmentId")?.SetValue(person, Id("department:" + item.Department));
            typeof(Device).GetProperty("DepartmentId")?.SetValue(device, Id("department:" + item.Department));
            db.Users.Add(person);
            db.Devices.Add(device);
            db.PolicyAssignments.Add(new PolicyAssignment { Id = Id("assignment:" + i), OrganizationId = orgId, DeviceId = device.Id, PolicyId = policy.Id });
            staff.Add(person);
            machines.Add(device);
            for (var sample = 0; sample < 12; sample++)
            {
                var at = historical.AddMinutes(-55 + sample * 5);
                db.Telemetry.Add(new TelemetrySnapshot
                {
                    Id = Id($"telemetry:{i}:{sample}"),
                    OrganizationId = orgId,
                    DeviceId = device.Id,
                    CpuPercent = 12 + (i * 9 + sample * 7) % 70,
                    RamPercent = 25 + (i * 7 + sample * 3) % 60,
                    DiskPercent = 31 + i * 4,
                    CreatedAt = at,
                    UpdatedAt = at,
                    CollectedAt = at
                });
            }
        }
        foreach (var (item, index) in input.Apps.Select((item, index) => (item, index)))
            db.SelfServiceCatalogApps.Add(new SelfServiceCatalogApp
            {
                Id = Id("app:" + index),
                OrganizationId = orgId,
                PublishedByUserId = adminId,
                Name = "[MẪU] " + item.Name,
                Version = item.Version,
                Description = item.Description + " Bộ cài thật đã kiểm tra SHA-256 và chữ ký. Dùng cho Windows x64; cần IT duyệt. Chưa kiểm thử cài bằng Agent.",
                PackageUrl = item.PackageUrl,
                Sha256 = item.Sha256,
                PublisherThumbprint = item.PublisherThumbprint,
                IsActive = true,
                RequiresApproval = true
            });
        for (var i = 0; i < input.Issues.Length; i++)
        {
            var item = input.Issues[i];
            var device = machines[item.Device];
            var person = staff[item.Device];
            var created = now.AddDays(-item.DaysAgo);
            var done = item.Status is "Resolved" or "Closed";
            var request = new SelfServiceRequest
            {
                Id = Id("request:" + i),
                OrganizationId = orgId,
                DeviceId = device.Id,
                UserId = person.Id,
                DeviceName = device.Name,
                UserName = person.DisplayName,
                Kind = item.Kind,
                Category = item.Category,
                Title = "[MẪU] " + item.Title,
                Description = "Tình huống giả lập, không phải báo lỗi từ máy thật. " + item.Description,
                CanWork = item.CanWork,
                Status = item.Status,
                AssignedTechnicianId = item.Status == "Open" ? null : adminId,
                AssignedTechnicianName = item.Status == "Open" ? null : "Quản trị hệ thống",
                CatalogAppId = item.Kind == "InstallApp" ? Id("app:" + (1 - i % 2)) : null,
                IdempotencyKey = dataset + ":request:" + i,
                RequestFingerprint = Hash(dataset + ":request:" + i),
                CreatedAt = created,
                UpdatedAt = done ? created.AddHours(4) : created.AddHours(1)
            };
            db.SelfServiceRequests.Add(request);
            db.SelfServiceMessages.Add(new SelfServiceMessage
            {
                Id = Id("message:employee:" + i),
                OrganizationId = orgId,
                RequestId = request.Id,
                AuthorId = person.Id,
                AuthorName = person.DisplayName,
                Body = "[MẪU] " + item.Description,
                IdempotencyKey = dataset + ":employee:" + i,
                CreatedAt = created,
                UpdatedAt = created
            });
            db.SelfServiceMessages.Add(new SelfServiceMessage
            {
                Id = Id("message:it:" + i),
                OrganizationId = orgId,
                RequestId = request.Id,
                AuthorId = adminId,
                AuthorName = "Quản trị hệ thống",
                Body = "[MẪU] " + item.Response,
                IdempotencyKey = dataset + ":it:" + i,
                CreatedAt = created.AddHours(1),
                UpdatedAt = created.AddHours(1)
            });
            if (item.Kind == "Incident")
            {
                db.Incidents.Add(new IncidentTicket
                {
                    Id = Id("incident:" + i),
                    OrganizationId = orgId,
                    DeviceId = device.Id,
                    ReportedByUserId = person.Id,
                    Title = request.Title,
                    Description = request.Description,
                    Severity = item.CanWork ? "Low" : "High",
                    Status = done ? "Resolved" : item.Status == "InProgress" ? "InProgress" : "Open",
                    AssignedTechnicianId = request.AssignedTechnicianId,
                    ResolvedAt = done ? created.AddHours(4) : null,
                    ResolutionNotes = done ? "[MẪU] " + item.Response : null,
                    CreatedAt = created,
                    UpdatedAt = request.UpdatedAt
                });
            }
        }
        for (var i = 0; i < 6; i++)
        {
            var done = i >= 4;
            db.Alerts.Add(new Alert
            {
                Id = Id("alert:" + i),
                OrganizationId = orgId,
                DeviceId = machines[i].Id,
                Severity = i % 2 == 0 ? "Warning" : "Info",
                Message = "[MẪU] " + input.Alerts[i],
                IsOpen = !done,
                CreatedAt = historical.AddHours(-i),
                AcknowledgedAt = i == 0 ? null : historical,
                ResolvedAt = done ? historical.AddHours(1) : null,
                ResolvedByUserId = done ? adminId : null
            });
        }
        for (var i = 0; i < 4; i++)
        {
            var completed = i >= 2;
            db.WorkOrders.Add(new WorkOrder
            {
                Id = Id("work:" + i),
                OrganizationId = orgId,
                DeviceId = machines[i + 4].Id,
                WorkOrderNumber = "SAMPLE-2026-" + (i + 1).ToString("000", CultureInfo.InvariantCulture),
                Title = "[MẪU] " + (i % 2 == 0 ? "Vệ sinh và kiểm tra máy định kỳ" : "Kiểm tra SSD và sao lưu dữ liệu"),
                Type = "Preventive",
                Priority = "Medium",
                Status = completed ? "Completed" : "Scheduled",
                DueDate = completed ? historical : now.AddDays(i + 1),
                CompletedAt = completed ? historical.AddHours(1) : null,
                AssignedTechnicianId = adminId,
                LaborHours = completed ? 1 : 0,
                LaborCost = completed ? 150000 : 0,
                PartsCost = 0,
                Notes = "Dữ liệu bảo trì giả lập; không thực hiện thay đổi trên máy thật.",
                CreatedAt = historical.AddDays(-1)
            });
        }
        db.SelfServiceAnnouncements.Add(new SelfServiceAnnouncement
        {
            Id = Id("announcement"),
            OrganizationId = orgId,
            Title = "[MẪU] Hướng dẫn dùng bộ dữ liệu văn phòng",
            Body = "Các máy, nhân viên và báo lỗi có tiền tố [MẪU] là dữ liệu giả lập. Máy mẫu chưa có Agent và không báo trực tuyến. Không dùng tài khoản mẫu để đăng nhập. Bạn có thể tạo nhân viên/máy thật riêng để kiểm thử. Danh mục phần mềm chứa bộ cài thật, luôn cần IT duyệt.",
            IsOutage = false,
            RequiresAcknowledgement = false,
            StartsAt = now.AddMinutes(-1),
            EndsAt = now.AddDays(30),
            PublishedAt = now
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = markerId,
            OrganizationId = orgId,
            ActorId = adminId,
            Action = "CompanySampleDatasetCreated",
            Reason = $"{dataset}: User requested internal office sample dataset; confirmed=true; nonce={nonce}; expiresAt={expiry:O}. All sample users locked; no device credentials, commands, enrollments or live heartbeats created.",
            Outcome = "Success"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        await Verify();

        async Task Verify()
        {
            var deviceIds = Enumerable.Range(0, 12).Select(i => Id("device:" + i)).ToArray();
            var userIds = Enumerable.Range(0, 12).Select(i => Id("user:" + i)).ToArray();
            var requestIds = Enumerable.Range(0, 12).Select(i => Id("request:" + i)).ToArray();
            var devices = await db.Devices.Where(x => x.OrganizationId == orgId && deviceIds.Contains(x.Id)).ToListAsync(ct);
            var people = await db.Users.Where(x => x.OrganizationId == orgId && userIds.Contains(x.Id)).ToListAsync(ct);
            var requests = await db.SelfServiceRequests.Where(x => x.OrganizationId == orgId && requestIds.Contains(x.Id)).ToListAsync(ct);
            var catalogIds = new[] { Id("app:0"), Id("app:1") };
            var apps = await db.SelfServiceCatalogApps.Where(x => x.OrganizationId == orgId && catalogIds.Contains(x.Id)).ToListAsync(ct);
            if (devices.Count != 12 || devices.Any(x => x.LastSeenAt != null || x.AgentVersion != "sample-only" || !x.Name.StartsWith("[MẪU]", StringComparison.Ordinal)) ||
                people.Count != 12 || people.Any(x => x.Status != "Locked" || x.Role != "Employee") ||
                requests.Count != 12 || requests.Any(x => !x.Title.StartsWith("[MẪU]", StringComparison.Ordinal) || x.CommandId != null) || apps.Count != 2 ||
                await db.DeviceCredentials.AnyAsync(x => x.OrganizationId == orgId && deviceIds.Contains(x.DeviceId), ct) ||
                await db.Commands.AnyAsync(x => x.OrganizationId == orgId && deviceIds.Contains(x.DeviceId), ct) ||
                !await db.AuditLogs.AnyAsync(x => x.Id == markerId && x.OrganizationId == orgId, ct))
                throw new InvalidOperationException("Sample dataset verification failed.");
            var telemetry = await db.Telemetry.CountAsync(x => x.OrganizationId == orgId && deviceIds.Contains(x.DeviceId), ct);
            var messages = await db.SelfServiceMessages.Where(x => x.OrganizationId == orgId && requestIds.Contains(x.RequestId)).ToListAsync(ct);
            if (telemetry != 144 || messages.Count != 24 || messages.Any(x => !x.Body.StartsWith("[MẪU]", StringComparison.Ordinal)))
                throw new InvalidOperationException("History/encryption round-trip verification failed.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                dataset,
                devices = devices.Count,
                lockedSampleEmployees = people.Count,
                historicalTelemetry = telemetry,
                supportRequests = requests.Count,
                activeRequests = requests.Count(x => x.Status is not ("Resolved" or "Closed")),
                resolvedOrClosedRequests = requests.Count(x => x.Status is "Resolved" or "Closed"),
                messages = messages.Count,
                catalogApps = apps.Count,
                realDeviceCredentialsCreated = 0,
                commandsCreated = 0
            }));
        }

        Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(orgId + ":" + dataset + ":" + key)).AsSpan(0, 16));
        string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException("Missing setting: " + name);
    }
}

public sealed record SampleInput(SampleDevice[] Devices, SampleIssue[] Issues, SampleApp[] Apps, string[] Alerts);
public sealed record SampleDevice(string Code, string Department, string Employee, string Os, string Manufacturer, string Model, string Type, string Floor, decimal Cost, string Cpu, string Ram, string Storage, string[] Applications);
public sealed record SampleIssue(int Device, string Kind, string Category, string Title, string Description, string Response, string Status, bool CanWork, int DaysAgo);
public sealed record SampleApp(string Name, string Version, string Description, string PackageUrl, string Sha256, string PublisherThumbprint);
public sealed class SampleTenant(Guid organizationId) : ICurrentTenantProvider
{
    public Guid? CurrentOrganizationId => organizationId;
}
