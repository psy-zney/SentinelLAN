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
        var admin = new User { OrganizationId = organization.Id, Email = getSetting("SENTINELLAN_DEMO_ADMIN_EMAIL") ?? "admin@sentinellan.local", DisplayName = "Demo Admin", Role = Roles.Admin, PasswordHash = passwordHasher.Hash(password), Status = UserStatuses.Active };
        var technician = new User { OrganizationId = organization.Id, Email = "technician@sentinellan.local", DisplayName = "Demo Technician", Role = Roles.Technician, PasswordHash = passwordHasher.Hash(password), Status = UserStatuses.Active };
        var employee = new User { OrganizationId = organization.Id, Email = "employee@sentinellan.local", DisplayName = "Demo Employee", Role = Roles.Employee, PasswordHash = passwordHasher.Hash(password), Status = UserStatuses.Active };
        var employeeDevice = new Device
        {
            OrganizationId = organization.Id,
            AssignedUserId = employee.Id,
            Name = "EMPLOYEE-DEMO-PC",
            OsVersion = "Windows 11 24H2",
            AgentVersion = "0.1.0",
            LastSeenAt = DateTimeOffset.UtcNow,
            SerialNumber = "DELL-SN-742099",
            Manufacturer = "Dell Inc.",
            Model = "Latitude 7420 vPro",
            AssetType = "Laptop",
            LocationCampus = "Cơ sở 1 (Khu Công Nghệ Cao)",
            LocationBuilding = "Tòa Alpha",
            LocationFloor = "Tầng 3",
            LocationRoom = "Lab IT 302",
            AssetStatus = "Active",
            PurchaseDate = DateTimeOffset.UtcNow.AddYears(-2).AddMonths(-2),
            PurchaseCost = 1450m,
            WarrantyExpiresAt = DateTimeOffset.UtcNow.AddMonths(10),
            VendorName = "Dell Vietnam Enterprise Partner",
            SpecificationsJson = "{\"cpu\":\"Intel Core i7-1185G7 @ 3.00GHz\",\"ram\":\"16GB LPDDR4x-4266\",\"storage\":\"512GB NVMe PCIe Gen4\",\"display\":\"14.0 FHD IPS 400 nits\"}"
        };

        var serverDevice = new Device
        {
            OrganizationId = organization.Id,
            AssignedUserId = technician.Id,
            Name = "SERVER-SRV-CORE01",
            OsVersion = "Ubuntu 24.04 LTS",
            AgentVersion = "0.1.0",
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            SerialNumber = "HPE-DL380-9988",
            Manufacturer = "Hewlett Packard Enterprise",
            Model = "ProLiant DL380 Gen10",
            AssetType = "Server",
            LocationCampus = "Cơ sở 1 (Khu Công Nghệ Cao)",
            LocationBuilding = "Trung Tâm Dữ Liệu DC",
            LocationFloor = "Tầng 1",
            LocationRoom = "Server Room Rack 02",
            AssetStatus = "Active",
            PurchaseDate = DateTimeOffset.UtcNow.AddYears(-3).AddMonths(-4),
            PurchaseCost = 4200m,
            WarrantyExpiresAt = DateTimeOffset.UtcNow.AddMonths(8),
            VendorName = "HPE Direct Distribution",
            SpecificationsJson = "{\"cpu\":\"2x Intel Xeon Silver 4210R (20 Cores)\",\"ram\":\"64GB ECC DDR4-2933\",\"storage\":\"4x 1.92TB Enterprise SAS SSD (RAID 10)\",\"network\":\"4x 10GbE SFP+\"}"
        };

        var standardPolicy = new Policy { OrganizationId = organization.Id, Name = "Standard Workstation", IdleTimeoutMinutes = 15, UsbMode = "ReadOnly" };
        var serverPolicy = new Policy { OrganizationId = organization.Id, Name = "Cloud Server (VPS)", IdleTimeoutMinutes = 60, UsbMode = "Blocked" };
        var sampleAlert = new Alert
        {
            OrganizationId = organization.Id,
            DeviceId = employeeDevice.Id,
            Severity = "Warning",
            Message = "Device CPU usage exceeded 85% threshold during scheduled maintenance scan.",
            IsOpen = true
        };

        var sampleIncident = new IncidentTicket
        {
            OrganizationId = organization.Id,
            DeviceId = employeeDevice.Id,
            Title = "Quạt tản nhiệt phát tiếng kêu lạ khi tải nặng",
            Description = "Người dùng phản hồi tiếng quạt kêu rè rè sau khi mở các phần mềm đồ họa nặng.",
            Severity = "Medium",
            Status = "Resolved",
            ReportedByUserId = employee.Id,
            AssignedTechnicianId = technician.Id,
            ResolvedAt = DateTimeOffset.UtcNow.AddDays(-5),
            ResolutionNotes = "Đã tháo máy, vệ sinh cánh quạt, tra dầu bôi trơn và thay keo tản nhiệt gốm mới."
        };

        var sampleIncidentOpen = new IncidentTicket
        {
            OrganizationId = organization.Id,
            DeviceId = employeeDevice.Id,
            Title = "Pin có dấu hiệu chai sau hơn 2 năm sử dụng",
            Description = "Dung lượng pin thực tế còn khoảng 72% so với dung lượng thiết kế, thời lượng dùng còn ~2.5 giờ.",
            Severity = "Low",
            Status = "Open",
            ReportedByUserId = employee.Id,
            AssignedTechnicianId = technician.Id
        };

        var sampleWorkOrder = new WorkOrder
        {
            OrganizationId = organization.Id,
            DeviceId = employeeDevice.Id,
            IncidentId = sampleIncident.Id,
            WorkOrderNumber = "WO-2026-0001",
            Title = "Bảo trì định kỳ và thay thế keo tản nhiệt",
            Type = "Preventive",
            Priority = "Medium",
            Status = "Completed",
            DueDate = DateTimeOffset.UtcNow.AddDays(-6),
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-5),
            LaborHours = 1.5,
            PartsCost = 15.0m,
            LaborCost = 45.0m,
            ChecklistJson = "[\"Vệ sinh bụi toàn bộ mainboard\",\"Kiểm tra quạt làm mát\",\"Tra keo MX-4\",\"Chạy bài test nhiệt độ AIDA64\"]",
            Notes = "Nhiệt độ sau bảo trì giảm từ 89°C xuống còn 68°C ở mức tải 100%.",
            AssignedTechnicianId = technician.Id
        };

        var sampleTelemetry1 = new TelemetrySnapshot
        {
            OrganizationId = organization.Id,
            DeviceId = employeeDevice.Id,
            CpuPercent = 38.5,
            RamPercent = 64.2,
            DiskPercent = 52.0
        };

        var sampleTelemetry2 = new TelemetrySnapshot
        {
            OrganizationId = organization.Id,
            DeviceId = serverDevice.Id,
            CpuPercent = 22.0,
            RamPercent = 48.6,
            DiskPercent = 41.2
        };

        db.AddRange(
            organization,
            admin,
            technician,
            employee,
            employeeDevice,
            serverDevice,
            standardPolicy,
            serverPolicy,
            sampleAlert,
            sampleIncident,
            sampleIncidentOpen,
            sampleWorkOrder,
            sampleTelemetry1,
            sampleTelemetry2,
            new PolicyAssignment { OrganizationId = organization.Id, PolicyId = standardPolicy.Id, DeviceId = employeeDevice.Id },
            new PolicyAssignment { OrganizationId = organization.Id, PolicyId = serverPolicy.Id, DeviceId = serverDevice.Id });

        var token = getSetting("SENTINELLAN_ENROLLMENT_TOKEN") ?? "local-enroll-only";
        db.Add(new DeviceEnrollmentToken { OrganizationId = organization.Id, TokenHash = SecretHash.Create(token), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });

        var demoQrCode = "demo-qr-asset-employee-pc-2026";
        db.Add(new DeviceQrLabel
        {
            OrganizationId = organization.Id,
            DeviceId = employeeDevice.Id,
            CodeHash = SecretHash.Create(demoQrCode),
            CodePrefix = "qr-demo-emp",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
