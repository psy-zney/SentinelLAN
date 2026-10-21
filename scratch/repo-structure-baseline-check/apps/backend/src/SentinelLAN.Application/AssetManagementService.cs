using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class AssetManagementService(IAssetStore store) : IAssetManagementService
{
    public async Task<DeviceAssetDetailDto?> GetDeviceAssetDetailAsync(ActorContext actor, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return null;

        if (actor.Role == Roles.Employee && (device.AssignedUserId != actor.UserId || device.IsRevoked))
            return null;

        var assignedUserName = await store.GetUserNameAsync(actor.OrganizationId, device.AssignedUserId, cancellationToken);
        var recentTelemetry = await store.GetRecentTelemetryAsync(actor.OrganizationId, deviceId, 10, cancellationToken);
        var incidents = await store.GetIncidentsAsync(actor.OrganizationId, deviceId, cancellationToken);
        var workOrders = await store.GetWorkOrdersAsync(actor.OrganizationId, deviceId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var health = CalculateHealthScore(device, recentTelemetry, incidents, now);
        var repairVsReplace = CalculateRepairVsReplace(device, workOrders, now);

        return new DeviceAssetDetailDto(
            device.Id,
            device.Name,
            device.OsVersion,
            device.AgentVersion,
            device.LastSeenAt,
            device.IsOnline(now),
            device.AssignedUserId,
            assignedUserName,
            device.IsRevoked,
            device.SerialNumber,
            device.Manufacturer,
            device.Model,
            device.AssetType,
            device.LocationCampus,
            device.LocationBuilding,
            device.LocationFloor,
            device.LocationRoom,
            device.AssetStatus,
            device.PurchaseDate,
            device.PurchaseCost,
            device.WarrantyExpiresAt,
            device.VendorName,
            device.SpecificationsJson,
            health,
            repairVsReplace
        );
    }

    public async Task<(ManagementResultStatus Status, string Message)> UpdateAssetProfileAsync(
        ActorContext actor, Guid deviceId, UpdateAssetProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role != Roles.Admin && actor.Role != Roles.Technician)
            return (ManagementResultStatus.Forbidden, "Only Admin and Technician can update asset profile.");

        if (string.IsNullOrWhiteSpace(request.Reason) || !request.Confirmed)
            return (ManagementResultStatus.Invalid, "A valid confirmation and reason are required.");

        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return (ManagementResultStatus.NotFound, "Device not found.");

        if (request.SerialNumber is not null) device.SerialNumber = request.SerialNumber.Trim();
        if (request.Manufacturer is not null) device.Manufacturer = request.Manufacturer.Trim();
        if (request.Model is not null) device.Model = request.Model.Trim();
        if (request.AssetType is not null) device.AssetType = request.AssetType.Trim();
        if (request.LocationCampus is not null) device.LocationCampus = request.LocationCampus.Trim();
        if (request.LocationBuilding is not null) device.LocationBuilding = request.LocationBuilding.Trim();
        if (request.LocationFloor is not null) device.LocationFloor = request.LocationFloor.Trim();
        if (request.LocationRoom is not null) device.LocationRoom = request.LocationRoom.Trim();
        if (!string.IsNullOrWhiteSpace(request.AssetStatus)) device.AssetStatus = request.AssetStatus.Trim();
        if (request.PurchaseDate.HasValue) device.PurchaseDate = request.PurchaseDate;
        if (request.PurchaseCost.HasValue) device.PurchaseCost = request.PurchaseCost;
        if (request.WarrantyExpiresAt.HasValue) device.WarrantyExpiresAt = request.WarrantyExpiresAt;
        if (request.VendorName is not null) device.VendorName = request.VendorName.Trim();
        if (request.SpecificationsJson is not null) device.SpecificationsJson = request.SpecificationsJson.Trim();

        await store.UpdateDeviceAsync(device, cancellationToken);
        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "AssetProfileUpdated",
            Reason = request.Reason.Trim(),
            Outcome = "Success"
        }, cancellationToken);

        return (ManagementResultStatus.Succeeded, "Asset profile updated successfully.");
    }

    public async Task<IReadOnlyList<AssetTimelineItemDto>> GetDeviceTimelineAsync(
        ActorContext actor, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await store.FindDeviceAsync(actor.OrganizationId, deviceId, cancellationToken);
        if (device is null) return [];

        if (actor.Role == Roles.Employee && (device.AssignedUserId != actor.UserId || device.IsRevoked))
            return [];

        var timeline = new List<AssetTimelineItemDto>();

        // 1. Enrollment / Registration
        timeline.Add(new AssetTimelineItemDto(
            $"reg-{device.Id}",
            device.CreatedAt,
            "Enrolled",
            "Thiết bị được khởi tạo và kích hoạt trên hệ thống",
            $"Khởi tạo với hệ điều hành {device.OsVersion}, Agent v{device.AgentVersion}",
            "info"
        ));

        // 2. Purchase Date if available
        if (device.PurchaseDate.HasValue)
        {
            timeline.Add(new AssetTimelineItemDto(
                $"purch-{device.Id}",
                device.PurchaseDate.Value,
                "Purchased",
                "Mua sắm & Bàn giao tài sản",
                $"Nguyên giá: {device.PurchaseCost:N0} USD, Nhà cung cấp: {device.VendorName ?? "N/A"}",
                "success"
            ));
        }

        // 3. Incidents
        var incidents = await store.GetIncidentsAsync(actor.OrganizationId, deviceId, cancellationToken);
        foreach (var inc in incidents)
        {
            var repName = await store.GetUserNameAsync(actor.OrganizationId, inc.ReportedByUserId, cancellationToken);
            timeline.Add(new AssetTimelineItemDto(
                $"inc-{inc.Id}",
                inc.CreatedAt,
                "Incident",
                $"Sự cố báo hỏng: {inc.Title} [{inc.Severity}]",
                inc.Description ?? "Không có mô tả chi tiết",
                inc.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? "critical" : "warning",
                repName
            ));

            if (inc.ResolvedAt.HasValue)
            {
                timeline.Add(new AssetTimelineItemDto(
                    $"inc-res-{inc.Id}",
                    inc.ResolvedAt.Value,
                    "IncidentResolved",
                    $"Sự cố đã khắc phục: {inc.Title}",
                    inc.ResolutionNotes ?? "Đã xử lý xong",
                    "success"
                ));
            }
        }

        // 4. Work Orders
        var workOrders = await store.GetWorkOrdersAsync(actor.OrganizationId, deviceId, cancellationToken);
        foreach (var wo in workOrders)
        {
            timeline.Add(new AssetTimelineItemDto(
                $"wo-{wo.Id}",
                wo.CreatedAt,
                "WorkOrder",
                $"Phiếu bảo trì {wo.WorkOrderNumber}: {wo.Title} [{wo.Type}]",
                $"Độ ưu tiên: {wo.Priority}, Trạng thái: {wo.Status}",
                "info"
            ));

            if (wo.CompletedAt.HasValue)
            {
                timeline.Add(new AssetTimelineItemDto(
                    $"wo-comp-{wo.Id}",
                    wo.CompletedAt.Value,
                    "WorkOrderCompleted",
                    $"Hoàn thành bảo trì {wo.WorkOrderNumber}",
                    $"Chi phí: {wo.TotalCost:N0} USD (Linh kiện: {wo.PartsCost:N0}, Giờ công: {wo.LaborCost:N0} - {wo.LaborHours}h)",
                    "success"
                ));
            }
        }

        // 5. Commands
        var commands = await store.GetDeviceCommandsAsync(actor.OrganizationId, deviceId, cancellationToken);
        foreach (var cmd in commands.Take(15))
        {
            timeline.Add(new AssetTimelineItemDto(
                $"cmd-{cmd.Id}",
                cmd.CreatedAt,
                "Command",
                $"Lệnh an toàn: {cmd.Type}",
                $"Lý do: {cmd.Reason} - Kết quả: {cmd.Status}",
                cmd.Status == DeviceCommandStatus.Succeeded ? "info" : "warning"
            ));
        }

        // 6. Loans
        var loans = await store.GetAssetLoansAsync(actor.OrganizationId, deviceId, cancellationToken);
        foreach (var loan in loans)
        {
            var borrower = await store.GetUserNameAsync(actor.OrganizationId, loan.BorrowerUserId, cancellationToken);
            timeline.Add(new AssetTimelineItemDto(
                $"loan-{loan.Id}",
                loan.BorrowedAt,
                "Loan",
                $"Bàn giao mượn thiết bị cho {borrower ?? "Nhân viên"}",
                $"Tình trạng ban đầu: {loan.ConditionBefore ?? "Bình thường"}",
                "info"
            ));

            if (loan.ReturnedAt.HasValue)
            {
                timeline.Add(new AssetTimelineItemDto(
                    $"loan-ret-{loan.Id}",
                    loan.ReturnedAt.Value,
                    "LoanReturned",
                    $"Hoàn trả thiết bị",
                    $"Tình trạng sau hoàn trả: {loan.ConditionAfter ?? "Tốt"}",
                    "success"
                ));
            }
        }

        return timeline.OrderByDescending(t => t.Timestamp).ToList();
    }

    public async Task<IReadOnlyList<IncidentDto>> GetIncidentsAsync(ActorContext actor, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var incidents = await store.GetIncidentsAsync(actor.OrganizationId, deviceId, cancellationToken);
        var result = new List<IncidentDto>(incidents.Count);

        foreach (var inc in incidents)
        {
            var dev = await store.FindDeviceAsync(actor.OrganizationId, inc.DeviceId, cancellationToken);
            if (actor.Role == Roles.Employee && (dev?.AssignedUserId != actor.UserId || dev?.IsRevoked == true))
                continue;

            var reportedBy = await store.GetUserNameAsync(actor.OrganizationId, inc.ReportedByUserId, cancellationToken);
            var tech = await store.GetUserNameAsync(actor.OrganizationId, inc.AssignedTechnicianId, cancellationToken);

            result.Add(new IncidentDto(
                inc.Id,
                inc.DeviceId,
                dev?.Name ?? "Unknown Device",
                inc.Title,
                inc.Description,
                inc.Severity,
                inc.Status,
                inc.ReportedByUserId,
                reportedBy,
                inc.AssignedTechnicianId,
                tech,
                inc.ResolvedAt,
                inc.ResolutionNotes,
                inc.CreatedAt
            ));
        }

        return result.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<(ManagementResultStatus Status, IncidentDto? Incident, string Message)> CreateIncidentAsync(
        ActorContext actor, CreateIncidentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length is < 3 or > 200 ||
            request.Description?.Length > 4000 ||
            request.Severity is not ("Low" or "Medium" or "High" or "Critical") ||
            !IncidentIdempotency.IsValidKey(request.IdempotencyKey))
            return (ManagementResultStatus.Invalid, null, "A title of 3-200 characters, description of up to 4000 characters, valid severity and valid Idempotency-Key are required.");

        var device = await store.FindDeviceAsync(actor.OrganizationId, request.DeviceId, cancellationToken);
        if (device is null) return (ManagementResultStatus.NotFound, null, "Device not found.");

        if (actor.Role == Roles.Employee && (device.AssignedUserId != actor.UserId || device.IsRevoked))
            return (ManagementResultStatus.Forbidden, null, "You can only report incidents for your assigned device.");

        var fingerprint = IncidentIdempotency.Fingerprint(request.DeviceId, request.Title, request.Description, request.Severity);
        var prior = await store.FindIncidentByIdempotencyKeyAsync(actor.OrganizationId, actor.UserId, request.IdempotencyKey!, cancellationToken);
        if (prior is not null)
        {
            if (prior.RequestFingerprint != fingerprint)
                return (ManagementResultStatus.Conflict, null, "Idempotency-Key was already used with a different incident payload.");
            var replay = await ToIncidentDtoAsync(prior, device.Name, cancellationToken);
            return (ManagementResultStatus.Succeeded, replay, "Incident was already reported.");
        }

        var incident = new IncidentTicket
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = request.DeviceId,
            IdempotencyKey = request.IdempotencyKey!.Trim(),
            RequestFingerprint = fingerprint,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Severity = request.Severity,
            Status = "Open",
            ReportedByUserId = actor.UserId
        };

        var creation = await store.CreateIncidentAsync(incident, new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "IncidentReported",
            Reason = $"Reported incident: {incident.Title} ({incident.Severity})",
            Outcome = "Success"
        }, cancellationToken);
        if (creation.PayloadConflict)
            return (ManagementResultStatus.Conflict, null, "Idempotency-Key was already used with a different incident payload.");
        var created = creation.Incident;

        var reportedByName = await store.GetUserNameAsync(actor.OrganizationId, actor.UserId, cancellationToken);
        var dto = new IncidentDto(
            created.Id,
            created.DeviceId,
            device.Name,
            created.Title,
            created.Description,
            created.Severity,
            created.Status,
            created.ReportedByUserId,
            reportedByName,
            null,
            null,
            null,
            null,
            created.CreatedAt
        );

        return (ManagementResultStatus.Succeeded, dto, "Incident reported successfully.");
    }

    private async Task<IncidentDto> ToIncidentDtoAsync(IncidentTicket incident, string deviceName, CancellationToken cancellationToken)
    {
        var reportedByName = await store.GetUserNameAsync(incident.OrganizationId, incident.ReportedByUserId, cancellationToken);
        return new IncidentDto(incident.Id, incident.DeviceId, deviceName, incident.Title, incident.Description,
            incident.Severity, incident.Status, incident.ReportedByUserId, reportedByName,
            incident.AssignedTechnicianId, null, incident.ResolvedAt, incident.ResolutionNotes, incident.CreatedAt);
    }

    public async Task<(ManagementResultStatus Status, string Message)> UpdateIncidentStatusAsync(
        ActorContext actor, Guid incidentId, UpdateIncidentStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role != Roles.Admin && actor.Role != Roles.Technician)
            return (ManagementResultStatus.Forbidden, "Only Admin and Technician can update incident status.");

        if (request.Status is not ("Open" or "InProgress" or "Resolved" or "Closed") ||
            request.ResolutionNotes?.Length > 4000)
            return (ManagementResultStatus.Invalid, "Status or resolution notes are invalid.");

        if (request.AssignedTechnicianId.HasValue)
        {
            var technician = await store.FindUserAsync(actor.OrganizationId, request.AssignedTechnicianId.Value, cancellationToken);
            if (technician is null || technician.Role != Roles.Technician || technician.Status != UserStatuses.Active)
                return (ManagementResultStatus.Invalid, "Assigned technician must be an active Technician in this organization.");
        }

        var incident = await store.FindIncidentAsync(actor.OrganizationId, incidentId, cancellationToken);
        if (incident is null) return (ManagementResultStatus.NotFound, "Incident not found.");

        if (request.Status == "Resolved" || request.Status == "Closed")
        {
            incident.Resolve(DateTimeOffset.UtcNow, request.ResolutionNotes);
            incident.Status = request.Status;
        }
        else
        {
            incident.Status = request.Status;
        }

        if (request.AssignedTechnicianId.HasValue)
        {
            incident.AssignedTechnicianId = request.AssignedTechnicianId;
        }

        await store.UpdateIncidentAsync(incident, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = incident.DeviceId,
            Action = "IncidentStatusUpdated",
            Reason = $"Incident '{incident.Title}' status changed to {incident.Status}",
            Outcome = "Success"
        }, cancellationToken);

        return (ManagementResultStatus.Succeeded, "Incident updated successfully.");
    }

    public async Task<IReadOnlyList<WorkOrderDto>> GetWorkOrdersAsync(ActorContext actor, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var workOrders = await store.GetWorkOrdersAsync(actor.OrganizationId, deviceId, cancellationToken);
        var result = new List<WorkOrderDto>(workOrders.Count);

        foreach (var wo in workOrders)
        {
            var dev = await store.FindDeviceAsync(actor.OrganizationId, wo.DeviceId, cancellationToken);
            if (actor.Role == Roles.Employee && (dev?.AssignedUserId != actor.UserId || dev?.IsRevoked == true))
                continue;

            var tech = await store.GetUserNameAsync(actor.OrganizationId, wo.AssignedTechnicianId, cancellationToken);

            result.Add(new WorkOrderDto(
                wo.Id,
                wo.DeviceId,
                dev?.Name ?? "Unknown Device",
                wo.IncidentId,
                wo.WorkOrderNumber,
                wo.Title,
                wo.Type,
                wo.Priority,
                wo.Status,
                wo.DueDate,
                wo.CompletedAt,
                wo.LaborHours,
                wo.PartsCost,
                wo.LaborCost,
                wo.TotalCost,
                wo.ChecklistJson,
                wo.Notes,
                wo.AssignedTechnicianId,
                tech,
                wo.CreatedAt
            ));
        }

        return result
            .OrderBy(w => w.Status == "Completed" ? 1 : 0)
            .ThenByDescending(w => w.Priority switch { "Critical" => 4, "High" => 3, "Medium" => 2, "Low" => 1, _ => 0 })
            .ThenBy(w => w.DueDate ?? DateTimeOffset.MaxValue)
            .ThenBy(w => w.CreatedAt)
            .ToList();
    }

    public async Task<(ManagementResultStatus Status, WorkOrderDto? WorkOrder, string Message)> CreateWorkOrderAsync(
        ActorContext actor, CreateWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role != Roles.Admin && actor.Role != Roles.Technician)
            return (ManagementResultStatus.Forbidden, null, "Only Admin and Technician can create Work Orders.");

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length is < 3 or > 200 ||
            request.Type is not ("Corrective" or "Preventive") ||
            request.Priority is not ("Low" or "Medium" or "High" or "Critical") ||
            request.ChecklistJson?.Length > 8000 || request.Notes?.Length > 4000)
            return (ManagementResultStatus.Invalid, null, "Work order title, type, priority or notes are invalid.");

        var device = await store.FindDeviceAsync(actor.OrganizationId, request.DeviceId, cancellationToken);
        if (device is null) return (ManagementResultStatus.NotFound, null, "Device not found.");

        if (request.IncidentId.HasValue)
        {
            var incident = await store.FindIncidentAsync(actor.OrganizationId, request.IncidentId.Value, cancellationToken);
            if (incident is null || incident.DeviceId != device.Id)
                return (ManagementResultStatus.NotFound, null, "Incident not found for this device.");
        }
        if (request.AssignedTechnicianId.HasValue)
        {
            var technician = await store.FindUserAsync(actor.OrganizationId, request.AssignedTechnicianId.Value, cancellationToken);
            if (technician is null || technician.Role != Roles.Technician || technician.Status != UserStatuses.Active)
                return (ManagementResultStatus.Invalid, null, "Assigned technician must be an active Technician in this organization.");
        }

        var woNumber = $"WO-{DateTime.UtcNow.Year}-{Guid.NewGuid():N}";

        var wo = new WorkOrder
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = request.DeviceId,
            IncidentId = request.IncidentId,
            WorkOrderNumber = woNumber,
            Title = request.Title.Trim(),
            Type = request.Type,
            Priority = request.Priority,
            Status = "Scheduled",
            DueDate = request.DueDate,
            ChecklistJson = request.ChecklistJson,
            Notes = request.Notes?.Trim(),
            AssignedTechnicianId = request.AssignedTechnicianId ?? (actor.Role == Roles.Technician ? actor.UserId : null)
        };

        var created = await store.CreateWorkOrderAsync(wo, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "WorkOrderCreated",
            Reason = $"Created Work Order {created.WorkOrderNumber}: {created.Title}",
            Outcome = "Success"
        }, cancellationToken);

        var techName = await store.GetUserNameAsync(actor.OrganizationId, created.AssignedTechnicianId, cancellationToken);

        var dto = new WorkOrderDto(
            created.Id,
            created.DeviceId,
            device.Name,
            created.IncidentId,
            created.WorkOrderNumber,
            created.Title,
            created.Type,
            created.Priority,
            created.Status,
            created.DueDate,
            created.CompletedAt,
            created.LaborHours,
            created.PartsCost,
            created.LaborCost,
            created.TotalCost,
            created.ChecklistJson,
            created.Notes,
            created.AssignedTechnicianId,
            techName,
            created.CreatedAt
        );

        return (ManagementResultStatus.Succeeded, dto, "Work order created successfully.");
    }

    public async Task<(ManagementResultStatus Status, string Message)> CompleteWorkOrderAsync(
        ActorContext actor, Guid workOrderId, CompleteWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role != Roles.Admin && actor.Role != Roles.Technician)
            return (ManagementResultStatus.Forbidden, "Only Admin and Technician can complete Work Orders.");

        if (request.LaborHours is < 0 or > 1000 || request.PartsCost < 0 || request.LaborCost < 0 ||
            request.Notes?.Length > 4000)
            return (ManagementResultStatus.Invalid, "Work order costs, hours or notes are invalid.");

        var wo = await store.FindWorkOrderAsync(actor.OrganizationId, workOrderId, cancellationToken);
        if (wo is null) return (ManagementResultStatus.NotFound, "Work Order not found.");
        if (wo.Status == "Completed") return (ManagementResultStatus.Conflict, "Work Order is already completed.");

        wo.Complete(DateTimeOffset.UtcNow, request.LaborHours, request.PartsCost, request.LaborCost, request.Notes);

        // If this work order resolves an associated incident, resolve it
        if (wo.IncidentId.HasValue)
        {
            var incident = await store.FindIncidentAsync(actor.OrganizationId, wo.IncidentId.Value, cancellationToken);
            if (incident is not null && incident.Status != "Resolved" && incident.Status != "Closed")
            {
                incident.Resolve(DateTimeOffset.UtcNow, $"Resolved via Work Order {wo.WorkOrderNumber}");
                await store.UpdateIncidentAsync(incident, cancellationToken);
            }
        }

        await store.UpdateWorkOrderAsync(wo, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = wo.DeviceId,
            Action = "WorkOrderCompleted",
            Reason = $"Completed Work Order {wo.WorkOrderNumber} (Total Cost: {wo.TotalCost:N0} USD)",
            Outcome = "Success"
        }, cancellationToken);

        return (ManagementResultStatus.Succeeded, "Work order completed successfully.");
    }

    public async Task<IReadOnlyList<AssetLoanDto>> GetAssetLoansAsync(ActorContext actor, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var loans = await store.GetAssetLoansAsync(actor.OrganizationId, deviceId, cancellationToken);
        var result = new List<AssetLoanDto>(loans.Count);

        foreach (var loan in loans)
        {
            var dev = await store.FindDeviceAsync(actor.OrganizationId, loan.DeviceId, cancellationToken);
            var borrower = await store.GetUserNameAsync(actor.OrganizationId, loan.BorrowerUserId, cancellationToken);

            result.Add(new AssetLoanDto(
                loan.Id,
                loan.DeviceId,
                dev?.Name ?? "Unknown Device",
                loan.BorrowerUserId,
                borrower,
                loan.Status,
                loan.BorrowedAt,
                loan.ExpectedReturnDate,
                loan.ReturnedAt,
                loan.ConditionBefore,
                loan.ConditionAfter,
                loan.ApprovedByUserId,
                loan.Notes,
                loan.CreatedAt
            ));
        }

        return result.OrderByDescending(l => l.CreatedAt).ToList();
    }

    public async Task<(ManagementResultStatus Status, AssetLoanDto? Loan, string Message)> CreateAssetLoanAsync(
        ActorContext actor, CreateLoanRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role != Roles.Admin && actor.Role != Roles.Technician)
            return (ManagementResultStatus.Forbidden, null, "Only Admin and Technician can create asset loans.");

        var device = await store.FindDeviceAsync(actor.OrganizationId, request.DeviceId, cancellationToken);
        if (device is null) return (ManagementResultStatus.NotFound, null, "Device not found.");

        if (device.IsRevoked || request.ExpectedReturnDate <= DateTimeOffset.UtcNow ||
            request.ConditionBefore?.Length > 2000 || request.Notes?.Length > 4000)
            return (ManagementResultStatus.Invalid, null, "Active device, future return date and valid notes are required.");
        var borrower = await store.FindUserAsync(actor.OrganizationId, request.BorrowerUserId, cancellationToken);
        if (borrower is null || borrower.Role != Roles.Employee || borrower.Status != UserStatuses.Active)
            return (ManagementResultStatus.Invalid, null, "Borrower must be an active Employee in this organization.");
        var loans = await store.GetAssetLoansAsync(actor.OrganizationId, device.Id, cancellationToken);
        if (loans.Any(loan => loan.Status == "Active"))
            return (ManagementResultStatus.Conflict, null, "Device already has an active loan.");

        var loan = new AssetLoan
        {
            OrganizationId = actor.OrganizationId,
            DeviceId = request.DeviceId,
            BorrowerUserId = request.BorrowerUserId,
            Status = "Active",
            BorrowedAt = DateTimeOffset.UtcNow,
            ExpectedReturnDate = request.ExpectedReturnDate,
            ConditionBefore = request.ConditionBefore?.Trim(),
            Notes = request.Notes?.Trim(),
            ApprovedByUserId = actor.UserId
        };

        device.AssetStatus = "Loaned";
        await store.UpdateDeviceAsync(device, cancellationToken);

        var created = await store.CreateAssetLoanAsync(loan, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = device.Id,
            Action = "AssetLoanCreated",
            Reason = $"Loaned device to user {request.BorrowerUserId}",
            Outcome = "Success"
        }, cancellationToken);

        var borrowerName = await store.GetUserNameAsync(actor.OrganizationId, request.BorrowerUserId, cancellationToken);
        var dto = new AssetLoanDto(
            created.Id,
            created.DeviceId,
            device.Name,
            created.BorrowerUserId,
            borrowerName,
            created.Status,
            created.BorrowedAt,
            created.ExpectedReturnDate,
            created.ReturnedAt,
            created.ConditionBefore,
            created.ConditionAfter,
            created.ApprovedByUserId,
            created.Notes,
            created.CreatedAt
        );

        return (ManagementResultStatus.Succeeded, dto, "Asset loan created successfully.");
    }

    public async Task<(ManagementResultStatus Status, string Message)> ReturnAssetLoanAsync(
        ActorContext actor, Guid loanId, ReturnLoanRequest request, CancellationToken cancellationToken = default)
    {
        if (actor.Role != Roles.Admin && actor.Role != Roles.Technician)
            return (ManagementResultStatus.Forbidden, "Only Admin and Technician can process asset return.");

        var loan = await store.FindAssetLoanAsync(actor.OrganizationId, loanId, cancellationToken);
        if (loan is null) return (ManagementResultStatus.NotFound, "Loan not found.");
        if (loan.Status == "Returned") return (ManagementResultStatus.Conflict, "Loan was already returned.");

        loan.Return(DateTimeOffset.UtcNow, request.ConditionAfter);
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            loan.Notes = $"{loan.Notes} [Return Note: {request.Notes.Trim()}]".Trim();
        }

        var device = await store.FindDeviceAsync(actor.OrganizationId, loan.DeviceId, cancellationToken);
        if (device is not null)
        {
            device.AssetStatus = "Active";
            await store.UpdateDeviceAsync(device, cancellationToken);
        }

        await store.UpdateAssetLoanAsync(loan, cancellationToken);

        await store.RecordAuditAsync(new AuditLog
        {
            OrganizationId = actor.OrganizationId,
            ActorId = actor.UserId,
            DeviceId = loan.DeviceId,
            Action = "AssetLoanReturned",
            Reason = $"Asset returned with condition: {request.ConditionAfter ?? "Good"}",
            Outcome = "Success"
        }, cancellationToken);

        return (ManagementResultStatus.Succeeded, "Asset returned successfully.");
    }

    private static HealthScoreResultDto CalculateHealthScore(
        Device device, IReadOnlyList<TelemetrySnapshot> recentTelemetry, IReadOnlyList<IncidentTicket> incidents, DateTimeOffset now)
    {
        double cpuPen = 0, ramPen = 0, diskPen = 0, offlinePen = 0, agePen = 0, incPen = 0;
        var recommendations = new List<string>();

        // Offline penalty
        if (!device.IsOnline(now))
        {
            offlinePen = 25.0;
            recommendations.Add("Thiết bị đang Offline hoặc mất kết nối mạng.");
        }

        // Telemetry penalties
        if (recentTelemetry.Count > 0)
        {
            var avgCpu = recentTelemetry.Average(t => t.CpuPercent);
            var avgRam = recentTelemetry.Average(t => t.RamPercent);
            var avgDisk = recentTelemetry.Average(t => t.DiskPercent);

            if (avgCpu > 85.0) { cpuPen = 15.0; recommendations.Add("Tải CPU cao liên tục (>85%). Kiểm tra tiến trình ngầm."); }
            else if (avgCpu > 70.0) { cpuPen = 8.0; }

            if (avgRam > 90.0) { ramPen = 15.0; recommendations.Add("Bộ nhớ RAM gần đầy (>90%). Cân nhắc nâng cấp RAM."); }
            else if (avgRam > 80.0) { ramPen = 8.0; }

            if (avgDisk > 90.0) { diskPen = 20.0; recommendations.Add("Dung lượng ổ đĩa nguy cấp (>90%). Yêu cầu dọn dẹp bộ nhớ đệm."); }
            else if (avgDisk > 80.0) { diskPen = 10.0; }
        }

        // Age penalty
        var baseDate = device.PurchaseDate ?? device.CreatedAt;
        var ageYears = (now - baseDate).TotalDays / 365.25;
        if (ageYears >= 5.0) { agePen = 15.0; recommendations.Add("Thiết bị trên 5 năm tuổi, bước vào giai đoạn khấu hao hết."); }
        else if (ageYears >= 3.0) { agePen = 8.0; }

        // Incident penalty
        var openIncidents = incidents.Where(i => i.Status != "Resolved" && i.Status != "Closed").ToList();
        foreach (var inc in openIncidents)
        {
            incPen += inc.Severity.ToLowerInvariant() switch
            {
                "critical" => 25.0,
                "high" => 15.0,
                "medium" => 8.0,
                _ => 4.0
            };
        }
        if (incPen > 35.0) incPen = 35.0;

        if (openIncidents.Count > 0)
        {
            recommendations.Add($"Có {openIncidents.Count} sự cố hỏng hóc chưa xử lý dứt điểm.");
        }

        var totalDeductions = cpuPen + ramPen + diskPen + offlinePen + agePen + incPen;
        var finalScore = (int)Math.Clamp(Math.Round(100.0 - totalDeductions), 0, 100);

        var grade = finalScore switch
        {
            >= 85 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Fair",
            _ => "Critical"
        };

        if (recommendations.Count == 0)
        {
            recommendations.Add("Thiết bị hoạt động ổn định, thông số phần cứng trong ngưỡng an toàn.");
        }

        return new HealthScoreResultDto(
            finalScore,
            grade,
            cpuPen,
            ramPen,
            diskPen,
            offlinePen,
            agePen,
            incPen,
            recommendations
        );
    }

    private static RepairVsReplaceResultDto CalculateRepairVsReplace(
        Device device, IReadOnlyList<WorkOrder> workOrders, DateTimeOffset now)
    {
        var cumulativeCost = workOrders
            .Where(w => w.Status == "Completed")
            .Sum(w => w.TotalCost);

        var purchaseCost = device.PurchaseCost ?? 1200m;
        if (purchaseCost <= 0) purchaseCost = 1200m;

        var baseDate = device.PurchaseDate ?? device.CreatedAt;
        var ageYears = Math.Max(0.5, (now - baseDate).TotalDays / 365.25);

        var annualAvgMaintenance = cumulativeCost / (decimal)ageYears;
        // Project 3 years with age factor
        var ageMultiplier = ageYears >= 4.0 ? 1.4m : (ageYears >= 2.5 ? 1.2m : 1.0m);
        var estimated3Years = (annualAvgMaintenance * 3m * ageMultiplier) + 150m;

        var ratioPercent = (double)(cumulativeCost / purchaseCost) * 100.0;

        string recommendation;
        string summary;

        if (ratioPercent >= 55.0 || ageYears >= 5.0)
        {
            recommendation = "Replace Immediately";
            summary = $"Tổng chi phí bảo trì lũy kế ({cumulativeCost:N0} USD) đã vượt {ratioPercent:F1}% nguyên giá hoặc máy đã quá 5 năm tuổi. Thay mới sẽ tiết kiệm TCO hơn sửa chữa tiếp.";
        }
        else if (ratioPercent >= 35.0 || ageYears >= 3.5)
        {
            recommendation = "Evaluate Replacement";
            summary = $"Chi phí sửa chữa đạt {ratioPercent:F1}% nguyên giá. Nên đưa vào danh sách kiểm định định kỳ và chuẩn bị ngân sách thay thế trong 6-12 tháng tới.";
        }
        else
        {
            recommendation = "Keep & Maintain";
            summary = $"Tỷ lệ chi phí bảo dưỡng tối ưu ({ratioPercent:F1}%). Hiệu năng máy tính vẫn đảm bảo, tiếp tục duy trì bảo trì phòng ngừa.";
        }

        return new RepairVsReplaceResultDto(
            cumulativeCost,
            Math.Round(estimated3Years, 2),
            purchaseCost,
            Math.Round(ratioPercent, 1),
            recommendation,
            summary
        );
    }
}
