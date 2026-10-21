using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed class SoftwareComplianceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class SoftwareComplianceService(ISoftwareComplianceStore store, ISoftwareCatalogDetectionReader catalog,
    IAdministrativeRequestJournal journal, IAutomaticAlertStore alerts, IEndpointObservationReader devices, TimeProvider clock)
{
    public async Task<IReadOnlyList<SoftwareDetectionRuleDto>> DetectionRulesAsync(Guid org, CancellationToken ct)
    {
        var rules = await ActiveRulesAsync(org, ct);
        if (rules.Count > 256) throw new SoftwareComplianceException(409, "Tối đa 256 quy tắc nhận diện phần mềm đang hoạt động trong mỗi đơn vị.");
        return rules;
    }

    private async Task<IReadOnlyList<SoftwareDetectionRuleDto>> ActiveRulesAsync(Guid org, CancellationToken ct) =>
        (await catalog.GetRulesAsync(org, ct)).Concat((await store.GetProhibitedRulesAsync(org, ct)).Where(value => value.IsActive)
            .Select(value => new SoftwareDetectionRuleDto(value.Id, value.Name, "Prohibited", value.ProductCode, value.UpgradeCode, null, value.Revision)))
            .Where(value => value.ProductCode is not null || value.UpgradeCode is not null).ToArray();

    public async Task<IReadOnlyList<ProhibitedSoftwareRuleDto>> RulesAsync(ActorContext actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return (await store.GetProhibitedRulesAsync(actor.OrganizationId, ct)).OrderBy(value => value.Name).Select(RuleDto).ToArray();
    }

    public async Task<ProhibitedSoftwareRuleDto> SaveRuleAsync(ActorContext actor, Guid? id, SaveProhibitedSoftwareRuleRequest input, CancellationToken ct)
    {
        RequireAdmin(actor);
        var now = clock.GetUtcNow();
        if (!input.Confirmed || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 3 or > 1000 ||
            input.Nonce == Guid.Empty || input.ExpiresAt <= now || input.ExpiresAt > now.AddMinutes(5) ||
            string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120 || input.Name.Any(char.IsControl) ||
            input.ProductCode is null && input.UpgradeCode is null || input.ProductCode == Guid.Empty || input.UpgradeCode == Guid.Empty)
            throw new SoftwareComplianceException(400, "Cần tên, ProductCode hoặc UpgradeCode MSI, lý do, xác nhận và nonce còn hạn.");
        if (await journal.NonceExistsAsync(actor.OrganizationId, input.Nonce, ct)) throw new SoftwareComplianceException(409, "Thao tác đã được xử lý.");
        var value = id is null ? null : await store.FindRuleAsync(actor.OrganizationId, id.Value, ct);
        if (id is not null && value is null) throw new SoftwareComplianceException(404, "Không tìm thấy quy tắc.");
        if ((value?.Revision ?? Guid.Empty) != input.Revision) throw new SoftwareComplianceException(409, "Quy tắc đã thay đổi. Vui lòng tải lại.");
        if (input.IsActive)
        {
            var active = (await store.GetProhibitedRulesAsync(actor.OrganizationId, ct)).Count(rule => rule.IsActive && rule.Id != id);
            var approved = (await catalog.GetRulesAsync(actor.OrganizationId, ct)).Count(rule => rule.ProductCode is not null || rule.UpgradeCode is not null);
            if (active + approved >= 256) throw new SoftwareComplianceException(400, "Đơn vị đã đạt giới hạn 256 quy tắc nhận diện phần mềm.");
        }
        if (value is null) { value = new ProhibitedSoftwareRule { OrganizationId = actor.OrganizationId, Name = "" }; store.Add(value); }
        value.Name = input.Name.Trim(); value.ProductCode = input.ProductCode; value.UpgradeCode = input.UpgradeCode;
        value.IsActive = input.IsActive; value.Revision = Guid.NewGuid(); value.UpdatedAt = now;
        journal.Reserve(new AdministrativeRequestNonce { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, Nonce = input.Nonce, Operation = "SoftwareRuleSaved", ExpiresAt = input.ExpiresAt, CreatedAt = now });
        journal.Audit(new AuditLog { OrganizationId = actor.OrganizationId, ActorId = actor.UserId, Action = "SoftwareRuleSaved", Reason = input.Reason.Trim(), Outcome = value.IsActive ? "Active" : "Disabled", CreatedAt = now });
        if (!await journal.TrySaveAsync(ct)) throw new SoftwareComplianceException(409, "Có cập nhật đồng thời. Vui lòng tải lại quy tắc.");
        return RuleDto(value);
    }

    public async Task ApplyAgentReportAsync(AgentContext agent, IReadOnlyList<AgentSoftwareObservationDto>? report, CancellationToken ct)
    {
        if (report is null || report.Count == 0) return;
        var now = clock.GetUtcNow();
        if (report.Count > 256 || report.Any(value => value is null || value.RuleId == Guid.Empty || value.Source is not ("Catalog" or "Prohibited") ||
            value.Detection is not ("Installed" or "NotFound" or "Unknown" or "NotSupported") || value.InstalledVersion?.Length > 60 ||
            value.InstalledVersion?.Any(char.IsControl) == true || value.Detection != "Installed" && value.InstalledVersion is not null || value.RuleRevision == Guid.Empty ||
            value.CapturedAt < now.AddHours(-2) || value.CapturedAt > now.AddMinutes(1)) ||
            report.GroupBy(value => (value.RuleId, value.Source)).Any(group => group.Count() > 1))
            throw new SoftwareComplianceException(400, "Báo cáo phần mềm không hợp lệ hoặc quá giới hạn.");
        var rules = (await ActiveRulesAsync(agent.OrganizationId, ct)).ToDictionary(value => (value.Id, value.Source));
        var known = (await catalog.GetKnownIdsAsync(agent.OrganizationId, ct)).Select(id => (id, "Catalog"))
            .Concat((await store.GetProhibitedRulesAsync(agent.OrganizationId, ct)).Select(value => (value.Id, "Prohibited"))).ToHashSet();
        if (report.Any(value => !known.Contains((value.RuleId, value.Source)))) throw new SoftwareComplianceException(400, "Báo cáo chỉ được chứa quy tắc của đơn vị đã cấp cho Agent.");
        var existing = (await store.GetObservationsAsync(agent.OrganizationId, agent.DeviceId, ct)).ToDictionary(value => (value.RuleId, value.Source));
        var recentAlerts = await alerts.GetRecentAsync(agent.OrganizationId, agent.DeviceId, now.AddHours(-1), ct);
        foreach (var sample in report)
        {
            // Offline samples may describe a policy that was edited/retired meanwhile.
            if (!rules.TryGetValue((sample.RuleId, sample.Source), out var rule) || rule.Revision != sample.RuleRevision) continue;
            if (existing.TryGetValue((sample.RuleId, sample.Source), out var saved) && saved.CapturedAt > sample.CapturedAt) continue;
            if (saved is null)
            {
                saved = new DeviceSoftwareObservation
                {
                    OrganizationId = agent.OrganizationId,
                    DeviceId = agent.DeviceId,
                    RuleId = sample.RuleId,
                    Source = sample.Source,
                    Detection = sample.Detection
                };
                store.Add(saved);
            }
            saved.Detection = sample.Detection; saved.InstalledVersion = sample.InstalledVersion; saved.CapturedAt = sample.CapturedAt; saved.UpdatedAt = now; saved.RuleRevision = sample.RuleRevision;
            if (sample.Source != "Prohibited" || sample.CapturedAt < now.AddMinutes(-2)) continue;
            var key = $"ProhibitedSoftware:{sample.RuleId:N}";
            var open = recentAlerts.SingleOrDefault(value => value.AutomaticRule == key && value.IsOpen);
            if (sample.Detection == "NotFound" && open is not null)
            {
                open.Resolve(Guid.Empty, now); open.UpdatedAt = now;
                alerts.AddAudit(new AuditLog
                {
                    OrganizationId = agent.OrganizationId,
                    DeviceId = agent.DeviceId,
                    ActorId = Guid.Empty,
                    Action = "SoftwareComplianceRecovered",
                    Reason = "Mẫu mới không còn phát hiện phần mềm bị cấm đã cấu hình",
                    Outcome = "Resolved",
                    CreatedAt = now
                });
            }
            if (sample.Detection != "Installed" || open is not null || recentAlerts.Any(value => value.AutomaticRule == key && value.CreatedAt > now.AddHours(-1))) continue;
            var message = $"Phát hiện phần mềm thuộc quy tắc cấm: {rules[(sample.RuleId, sample.Source)].Name}. IT cần kiểm tra; hệ thống không tự gỡ phần mềm.";
            alerts.Add(new Alert { OrganizationId = agent.OrganizationId, DeviceId = agent.DeviceId, AutomaticRule = key, Severity = "Warning", Message = message, CreatedAt = now });
            alerts.AddSecurityEvent(new SecurityEvent { OrganizationId = agent.OrganizationId, DeviceId = agent.DeviceId, Type = "ProhibitedSoftware", Summary = message, CreatedAt = now });
            alerts.AddAudit(new AuditLog
            {
                OrganizationId = agent.OrganizationId,
                DeviceId = agent.DeviceId,
                ActorId = Guid.Empty,
                Action = "SoftwareComplianceAlerted",
                Reason = message,
                Outcome = "Open",
                CreatedAt = now
            });
        }
        // The Agent heartbeat unit of work commits its sample and these module changes atomically.
    }

    public async Task<IReadOnlyList<SoftwareComplianceDto>?> StatusAsync(ActorContext actor, Guid? deviceId, CancellationToken ct)
    {
        if (actor.Role is not (Roles.Admin or Roles.Employee)) throw new UnauthorizedAccessException();
        var device = actor.Role == Roles.Employee ? await devices.FindAssignedDeviceAsync(actor.OrganizationId, actor.UserId, ct)
            : deviceId is { } id ? await devices.FindDeviceAsync(actor.OrganizationId, id, ct) : null;
        if (device is null || device.IsRevoked || deviceId is { } requested && requested != device.Id) return null;
        var rules = (await catalog.GetRulesAsync(actor.OrganizationId, ct)).Concat((await store.GetProhibitedRulesAsync(actor.OrganizationId, ct))
            .Where(value => value.IsActive).Select(value => new SoftwareDetectionRuleDto(value.Id, value.Name, "Prohibited", value.ProductCode, value.UpgradeCode, null, value.Revision)));
        var observations = (await store.GetObservationsAsync(actor.OrganizationId, device.Id, ct)).ToDictionary(value => (value.RuleId, value.Source));
        return rules.OrderBy(value => value.Name).Select(rule =>
        {
            observations.TryGetValue((rule.Id, rule.Source), out var sample);
            var fresh = sample is not null && sample.RuleRevision == rule.Revision && sample.CapturedAt >= clock.GetUtcNow().AddMinutes(-30) && sample.CapturedAt <= clock.GetUtcNow().AddMinutes(1);
            var status = rule.ProductCode is null && rule.UpgradeCode is null ? "DetectionNotConfigured" : !fresh ? "Unknown" :
                sample!.Detection == "NotSupported" ? "NotSupported" : sample.Detection == "Unknown" ? "Unknown" :
                rule.Source == "Prohibited" ? sample.Detection == "Installed" ? "ProhibitedPresent" : "Compliant" :
                sample.Detection == "NotFound" ? "Missing" : VersionStatus(sample.InstalledVersion, rule.RequiredVersion);
            return new SoftwareComplianceDto(rule.Id, rule.Name, rule.Source, status, sample?.InstalledVersion, rule.RequiredVersion, sample?.CapturedAt);
        }).ToArray();
    }

    public static string VersionStatus(string? installed, string? required)
    {
        if (!Version.TryParse(installed, out var actual) || !Version.TryParse(required, out var target)) return "VersionUnknown";
        actual = new(actual.Major, actual.Minor, Math.Max(0, actual.Build), Math.Max(0, actual.Revision));
        target = new(target.Major, target.Minor, Math.Max(0, target.Build), Math.Max(0, target.Revision));
        return actual < target ? "UpdateAvailable" : "Installed";
    }

    private static ProhibitedSoftwareRuleDto RuleDto(ProhibitedSoftwareRule value) => new(value.Id, value.Name, value.ProductCode, value.UpgradeCode, value.IsActive, value.Revision);
    private static void RequireAdmin(ActorContext actor) { if (actor.Role != Roles.Admin) throw new UnauthorizedAccessException(); }
}
