"use client";

import React, { createContext, useContext, useEffect, useState } from "react";

export type Language = "vi" | "en";

export const translations = {
  vi: {
    // Navigation & Common
    appName: "SentinelLAN",
    appTagline: "Quản trị Thiết bị Đầu cuối & Node Đám mây",
    dashboard: "Bảng điều khiển",
    devices: "Thiết bị",
    policies: "Chính sách",
    commands: "Lệnh điều khiển",
    alerts: "Cảnh báo & Sự cố",
    auditLogs: "Nhật ký kiểm toán",
    users: "Người dùng & Truy cập",
    myDevice: "Thiết bị của tôi",
    logout: "Đăng xuất",
    language: "Ngôn ngữ",
    langName: "Tiếng Việt",
    actions: "Thao tác",
    status: "Trạng thái",
    created: "Thời gian tạo",
    all: "Tất cả",
    loading: "Đang tải dữ liệu...",
    save: "Lưu thay đổi",
    cancel: "Hủy bỏ",
    confirm: "Xác nhận",
    delete: "Xóa",
    retry: "Thử lại",
    refresh: "Làm mới",
    error: "Đã có lỗi xảy ra. Vui lòng kiểm tra lại kết nối và quyền truy cập.",
    none: "Không có",
    back: "Quay lại",
    filter: "Lọc",
    details: "Chi tiết",
    search: "Tìm kiếm...",
    timeUtc: "Thời gian (UTC)",

    // Status & Badges
    online: "Trực tuyến",
    offline: "Ngoại tuyến",
    pending: "Đang chờ",
    delivered: "Đã chuyển tiếp",
    succeeded: "Thành công",
    failed: "Thất bại",
    expired: "Hết hạn",
    open: "Đang mở",
    acknowledged: "Đã tiếp nhận",
    resolved: "Đã xử lý",
    critical: "Nghiêm trọng",
    warning: "Cảnh báo",
    info: "Thông tin",
    readOnly: "Chỉ đọc",
    blocked: "Bị chặn",
    fullAccess: "Toàn quyền",

    // Eyebrows & Subtitles
    eyebrowGovernance: "Quản trị",
    eyebrowOperations: "Vận hành",
    eyebrowMonitoring: "Giám sát",
    eyebrowCompliance: "Tuân thủ",
    eyebrowDirectory: "Danh bạ",
    eyebrowTransparency: "Minh bạch",

    subtitleDashboard: "Tổng quan trạng thái đội máy tính, cảnh báo đang mở và chỉ số phần cứng thời gian thực.",
    subtitlePolicies: "Khung thời gian làm việc, khóa màn hình khi rảnh, chế độ cổng USB và gán chính sách.",
    subtitleCommands: "Lệnh ngắn hạn được ký số mật mã với lý do bắt buộc, danh sách cho phép (allow-list) và kiểm toán bất biến.",
    subtitleAlerts: "Bất thường kết nối thời gian thực, vượt ngưỡng tài nguyên phần cứng và các sự kiện an ninh mạng.",
    subtitleAudit: "Nhật ký chỉ thêm (append-only) minh bạch cho các tác vụ nhạy cảm, điều khiển từ xa và thay đổi chính sách.",
    subtitleUsers: "Danh bạ người dùng đa tổ chức (multi-tenant), phân quyền RBAC và ranh giới quyền hạn.",
    subtitleMyDevice: "Xem minh bạch những thông số công ty theo dõi trên thiết bị của bạn. Không quay lén, không theo dõi phím gõ.",

    // Dashboard View
    totalDevices: "Tổng thiết bị",
    onlineDevices: "Đang trực tuyến",
    offlineDevices: "Ngoại tuyến",
    openAlerts: "Cảnh báo mở",
    managedDevices: "Danh mục thiết bị được quản lý",
    deviceName: "Tên thiết bị",
    osVersion: "Hệ điều hành",
    agentVersion: "Phiên bản Agent",
    lastSeen: "Lần cuối thấy",
    viewDetails: "Xem chi tiết",

    // Device Detail View
    telemetryHistory: "Lịch sử Telemetry",
    cpuUsage: "Sử dụng CPU",
    ramUsage: "Sử dụng RAM",
    diskUsage: "Sử dụng Ổ đĩa",
    noTelemetry: "Chưa có dữ liệu telemetry nào từ thiết bị này.",

    // Policies View
    policyList: "Danh sách chính sách bảo vệ",
    createPolicyBtn: "+ Tạo chính sách mới",
    policyNameLabel: "Tên chính sách",
    idleTimeoutLabel: "Thời gian khóa khi rảnh (phút)",
    usbModeLabel: "Chế độ cổng USB",
    assignedCountLabel: "Máy áp dụng",
    assignPolicyBtn: "Gán cho thiết bị",
    createPolicyModalTitle: "Tạo chính sách bảo vệ mới",
    assignPolicyModalTitle: "Gán chính sách cho thiết bị",
    selectDevicePrompt: "Chọn thiết bị áp dụng",
    policyCreatedSuccess: "Tạo chính sách thành công.",
    policyAssignedSuccess: "Gán chính sách cho thiết bị thành công.",
    saving: "Đang lưu...",

    // Commands View
    commandHistory: "Lịch sử thực thi lệnh",
    dispatchCommandBtn: "+ Phát lệnh điều khiển",
    commandModalTitle: "Phát lệnh điều khiển an toàn (Được ký số)",
    commandTypeLabel: "Loại lệnh điều khiển",
    commandReasonLabel: "Lý do phát lệnh (Bắt buộc)",
    commandReasonPlaceholder: "VD: Cập nhật cấu hình bảo mật định kỳ...",
    commandParameterLabel: "Tham số bổ sung (tùy chọn)",
    commandParameterPlaceholder: "VD: docker, nginx...",
    commandConfirmCheck: "Tôi xác nhận lệnh này tuân thủ chính sách và chịu trách nhiệm kiểm toán.",
    commandSignAndDispatch: "Ký số & Phát lệnh",
    commandDispatchedSuccess: "Lệnh đã được ký số HMAC và phát thành công.",
    dispatching: "Đang ký số...",
    commandTarget: "Thiết bị đích",
    commandType: "Loại lệnh",
    commandReason: "Lý do",
    commandStatus: "Trạng thái",
    commandIssuedBy: "Người phát",
    commandIssuedAt: "Thời điểm phát",
    commandExpiresAt: "Hết hạn lúc",
    commandResult: "Kết quả",

    // Alerts View
    alertSummary: "Tổng quan sự cố",
    triggerAlertBtn: "+ Kích hoạt cảnh báo thử nghiệm",
    triggerAlertModalTitle: "Tạo sự kiện cảnh báo an ninh",
    alertSeverityLabel: "Mức độ nghiêm trọng",
    alertMessageLabel: "Nội dung thông báo",
    alertMessagePlaceholder: "VD: Phát hiện truy cập tài nguyên bất thường...",
    alertDeviceLabel: "Thiết bị liên quan (tùy chọn)",
    alertTriggerSubmit: "Tạo & Phát cảnh báo",
    alertTriggerSuccess: "Đã tạo và phát cảnh báo an ninh.",
    ackBtn: "Tiếp nhận (Ack)",
    resolveBtn: "Giải quyết (Resolve)",
    alertAckSuccess: "Đã tiếp nhận xử lý cảnh báo.",
    alertResolveSuccess: "Cảnh báo đã được giải quyết.",
    noAlertsFound: "Không có cảnh báo nào.",

    // Audit View
    auditTrail: "Nhật ký kiểm toán tuân thủ (Append-only)",
    auditActor: "Người thực hiện",
    auditAction: "Hành động",
    auditDevice: "Thiết bị",
    auditReason: "Lý do",
    auditOutcome: "Kết quả",
    noAuditLogs: "Chưa có bản ghi nhật ký kiểm toán nào.",

    // Users View
    orgDetails: "Thông tin tổ chức",
    orgCode: "Mã tổ chức",
    orgName: "Tên tổ chức",
    userDirectory: "Danh sách người dùng",
    userEmail: "Email",
    userDisplayName: "Tên hiển thị",
    userRole: "Vai trò",
    rbacTitle: "Ma trận phân quyền (RBAC Policy Matrix)",

    // My Device View
    myDeviceHeader: "Thiết bị được giao cho bạn",
    transparencyBadge: "Minh bạch 100% (Privacy-First)",
    appliedPolicy: "Chính sách đang áp dụng",
    recentActionsTitle: "Nhật ký thao tác gần đây của IT trên máy bạn",
    noRecentActions: "Chưa có thao tác IT nào trên thiết bị này.",

    // Login Form
    signInTitle: "Đăng nhập SentinelLAN",
    signInSubtitle: "Hệ thống quản trị an toàn thiết bị đầu cuối và node đám mây",
    orgCodePrompt: "Mã định danh tổ chức",
    emailPrompt: "Email công việc",
    passwordPrompt: "Mật khẩu",
    signInBtn: "Đăng nhập",
    signingIn: "Đang xác thực...",
    loginError: "Mã tổ chức, email hoặc mật khẩu không chính xác.",

    // Cloud VPS
    vpsNodes: "Cloud VPS",
    eyebrowCloud: "Hạ tầng Cloud",
    subtitleVps: "Quản trị máy chủ Linux Cloud VPS từ xa qua SSH Private Key mã hóa AES-256 Vault. Giám sát tài nguyên và khởi động lại dịch vụ an toàn.",
    totalVpsNodes: "Tổng VPS Nodes",
    onlineVpsNodes: "VPS Trực tuyến",
    totalDockerContainers: "Docker Containers",
    avgVpsCpu: "CPU trung bình",
    avgVpsRam: "RAM trung bình",
    addVpsNodeBtn: "+ Thêm VPS Node",
    vpsNameLabel: "Tên máy chủ (Gợi nhớ)",
    vpsNamePlaceholder: "VD: Hanoi-Production-App...",
    vpsHostLabel: "Địa chỉ IPv4 / Host",
    vpsHostPlaceholder: "103.x.x.x hoặc vps.company.com",
    vpsPortLabel: "Cổng SSH",
    vpsUsernameLabel: "Tài khoản SSH",
    vpsUsernamePlaceholder: "root, ubuntu, debian...",
    vpsPrivateKeyLabel: "OpenSSH Private Key",
    vpsPrivateKeyPlaceholder: "-----BEGIN OPENSSH PRIVATE KEY-----\n...\n-----END OPENSSH PRIVATE KEY-----",
    vpsVaultNotice: "Khóa Private Key được mã hóa AES-256-GCM trong Vault. Chỉ giải mã tạm thời trong RAM khi thực thi lệnh và không bao giờ gửi lại qua API.",
    testConnectionBtn: "Kiểm tra kết nối SSH",
    testingConnection: "Đang kết nối SSH...",
    refreshMetricsBtn: "Làm mới số liệu",
    restartVpsServiceBtn: "Khởi động lại Service",
    vpsServiceRestartTitle: "Khởi động lại dịch vụ trên Cloud VPS",
    vpsServiceNameLabel: "Dịch vụ cần khởi động lại (Allow-listed)",
    vpsServiceReasonLabel: "Lý do thao tác (Bắt buộc)",
    vpsServiceReasonPlaceholder: "VD: Khắc phục sự cố Nginx trả về 502 Bad Gateway...",
    vpsRestartConfirmBtn: "Xác nhận & Khởi động lại",
    vpsUptime: "Thời gian hoạt động",
    vpsDockerRunning: "Container đang chạy",
    vpsLastChecked: "Kiểm tra lần cuối",
    vpsNoNodes: "Chưa có VPS Node nào được cấu hình. Bấm \"+ Thêm VPS Node\" để bắt đầu quản lý.",
    vpsDeleteConfirm: "Bạn có chắc chắn muốn xóa máy chủ VPS này không?"
  },

  en: {
    // Navigation & Common
    appName: "SentinelLAN",
    appTagline: "Secure Endpoint & Cloud Node Governance",
    dashboard: "Dashboard",
    devices: "Devices",
    policies: "Policies",
    commands: "Commands",
    alerts: "Alerts & Incidents",
    auditLogs: "Audit logs",
    users: "People & Access",
    myDevice: "My device",
    logout: "Sign out",
    language: "Language",
    langName: "English",
    actions: "Actions",
    status: "Status",
    created: "Created at",
    all: "All",
    loading: "Loading data...",
    save: "Save changes",
    cancel: "Cancel",
    confirm: "Confirm",
    delete: "Delete",
    retry: "Retry",
    refresh: "Refresh",
    error: "An error occurred. Check your connection and permissions.",
    none: "None",
    back: "Back",
    filter: "Filter",
    details: "Details",
    search: "Search...",
    timeUtc: "Timestamp (UTC)",

    // Status & Badges
    online: "Online",
    offline: "Offline",
    pending: "Pending",
    delivered: "Delivered",
    succeeded: "Succeeded",
    failed: "Failed",
    expired: "Expired",
    open: "Open",
    acknowledged: "Acknowledged",
    resolved: "Resolved",
    critical: "Critical",
    warning: "Warning",
    info: "Info",
    readOnly: "ReadOnly",
    blocked: "Blocked",
    fullAccess: "FullAccess",

    // Eyebrows & Subtitles
    eyebrowGovernance: "Governance",
    eyebrowOperations: "Operations",
    eyebrowMonitoring: "Monitoring",
    eyebrowCompliance: "Compliance",
    eyebrowDirectory: "Directory",
    eyebrowTransparency: "Transparency",

    subtitleDashboard: "Fleet status overview, active security incidents, and real-time hardware metrics.",
    subtitlePolicies: "Working hours, idle timeout lock, USB modes, and endpoint assignments.",
    subtitleCommands: "Short-lived cryptographically signed commands with reason, allow-list verification, and audit trail.",
    subtitleAlerts: "Realtime connectivity anomalies, metric violations, and security events requiring intervention.",
    subtitleAudit: "Append-only accountability logs for sensitive operations, command dispatch, and policy adjustments.",
    subtitleUsers: "Multi-tenant user identity, role assignments, and organizational access boundaries.",
    subtitleMyDevice: "Transparency view: inspect exactly what metrics and telemetry are monitored on your assigned device.",

    // Dashboard View
    totalDevices: "Total devices",
    onlineDevices: "Online",
    offlineDevices: "Offline",
    openAlerts: "Open alerts",
    managedDevices: "Managed Device Fleet",
    deviceName: "Device Name",
    osVersion: "OS Version",
    agentVersion: "Agent Version",
    lastSeen: "Last Seen",
    viewDetails: "View Details",

    // Device Detail View
    telemetryHistory: "Telemetry History",
    cpuUsage: "CPU Usage",
    ramUsage: "RAM Usage",
    diskUsage: "Disk Usage",
    noTelemetry: "No telemetry snapshots recorded yet for this device.",

    // Policies View
    policyList: "Security Policies",
    createPolicyBtn: "+ Create Policy",
    policyNameLabel: "Policy Name",
    idleTimeoutLabel: "Idle Timeout Lock (min)",
    usbModeLabel: "USB Storage Mode",
    assignedCountLabel: "Assigned Devices",
    assignPolicyBtn: "Assign to Device",
    createPolicyModalTitle: "Create Endpoint Policy",
    assignPolicyModalTitle: "Assign Policy to Device",
    selectDevicePrompt: "Select target device",
    policyCreatedSuccess: "Policy created successfully.",
    policyAssignedSuccess: "Policy assigned to device successfully.",
    saving: "Saving...",

    // Commands View
    commandHistory: "Command Dispatch History",
    dispatchCommandBtn: "+ Dispatch Command",
    commandModalTitle: "Dispatch Safe Signed Command",
    commandTypeLabel: "Command Type",
    commandReasonLabel: "Operational Reason (Mandatory)",
    commandReasonPlaceholder: "E.g., Scheduled maintenance health check...",
    commandParameterLabel: "Additional Parameter (Optional)",
    commandParameterPlaceholder: "E.g., docker, nginx...",
    commandConfirmCheck: "I confirm this command adheres to policy and accept full audit accountability.",
    commandSignAndDispatch: "Sign & Dispatch",
    commandDispatchedSuccess: "Command HMAC-signed and queued successfully.",
    dispatching: "Signing...",
    commandTarget: "Target Device",
    commandType: "Type",
    commandReason: "Reason",
    commandStatus: "Status",
    commandIssuedBy: "Issued By",
    commandIssuedAt: "Issued At",
    commandExpiresAt: "Expires At",
    commandResult: "Result",

    // Alerts View
    alertSummary: "Incident Summary",
    triggerAlertBtn: "+ Trigger Security Alert",
    triggerAlertModalTitle: "Trigger Security Alert Incident",
    alertSeverityLabel: "Severity Level",
    alertMessageLabel: "Alert Message",
    alertMessagePlaceholder: "E.g., Anomalous inbound connection detected...",
    alertDeviceLabel: "Target Device (Optional)",
    alertTriggerSubmit: "Trigger & Broadcast Alert",
    alertTriggerSuccess: "Security alert triggered and broadcasted.",
    ackBtn: "Ack",
    resolveBtn: "Resolve",
    alertAckSuccess: "Alert acknowledged successfully.",
    alertResolveSuccess: "Alert resolved successfully.",
    noAlertsFound: "No alerts found.",

    // Audit View
    auditTrail: "Tamper-Evident Audit Trail (Append-Only)",
    auditActor: "Actor",
    auditAction: "Action",
    auditDevice: "Device",
    auditReason: "Reason",
    auditOutcome: "Outcome",
    noAuditLogs: "No audit records found.",

    // Users View
    orgDetails: "Organization Profile",
    orgCode: "Organization Code",
    orgName: "Organization Name",
    userDirectory: "Authorized User Directory",
    userEmail: "Email Address",
    userDisplayName: "Display Name",
    userRole: "Role",
    rbacTitle: "Role-Based Access Control (RBAC) Matrix",

    // My Device View
    myDeviceHeader: "Your Assigned Corporate Device",
    transparencyBadge: "100% Transparent (Privacy-First)",
    appliedPolicy: "Applied Policy",
    recentActionsTitle: "Recent IT Administrator Actions on Your Device",
    noRecentActions: "No recent IT administrative actions logged.",

    // Login Form
    signInTitle: "Sign in to SentinelLAN",
    signInSubtitle: "Secure Endpoint & Cloud Node Governance",
    orgCodePrompt: "Organization Code",
    emailPrompt: "Work Email",
    passwordPrompt: "Password",
    signInBtn: "Sign In",
    signingIn: "Signing in...",
    loginError: "Invalid organization code, email, or password.",

    // Cloud VPS
    vpsNodes: "Cloud VPS",
    eyebrowCloud: "Cloud Infrastructure",
    subtitleVps: "Remote Linux Cloud VPS management via AES-256 Vault encrypted SSH private keys. Real-time telemetry monitoring and safe allow-listed service recovery.",
    totalVpsNodes: "Total VPS Nodes",
    onlineVpsNodes: "Online VPS",
    totalDockerContainers: "Docker Containers",
    avgVpsCpu: "Avg CPU",
    avgVpsRam: "Avg RAM",
    addVpsNodeBtn: "+ Add VPS Node",
    vpsNameLabel: "Server Name",
    vpsNamePlaceholder: "e.g. Hanoi-Production-App...",
    vpsHostLabel: "IPv4 Address / Host",
    vpsHostPlaceholder: "103.x.x.x or vps.company.com",
    vpsPortLabel: "SSH Port",
    vpsUsernameLabel: "SSH Username",
    vpsUsernamePlaceholder: "root, ubuntu, debian...",
    vpsPrivateKeyLabel: "OpenSSH Private Key",
    vpsPrivateKeyPlaceholder: "-----BEGIN OPENSSH PRIVATE KEY-----\n...\n-----END OPENSSH PRIVATE KEY-----",
    vpsVaultNotice: "Private keys are encrypted with AES-256-GCM in the Vault, decrypted ephemerally in RAM during SSH sessions, and never returned via the API.",
    testConnectionBtn: "Test SSH Connection",
    testingConnection: "Connecting via SSH...",
    refreshMetricsBtn: "Refresh Metrics",
    restartVpsServiceBtn: "Restart Service",
    vpsServiceRestartTitle: "Restart Service on Cloud VPS",
    vpsServiceNameLabel: "Service to restart (Allow-listed)",
    vpsServiceReasonLabel: "Reason for action (Required)",
    vpsServiceReasonPlaceholder: "e.g. Recovering crashed Nginx 502 Bad Gateway...",
    vpsRestartConfirmBtn: "Confirm & Restart",
    vpsUptime: "Uptime",
    vpsDockerRunning: "Running Containers",
    vpsLastChecked: "Last checked",
    vpsNoNodes: "No Cloud VPS nodes configured. Click \"+ Add VPS Node\" to register your first server.",
    vpsDeleteConfirm: "Are you sure you want to remove this Cloud VPS node?"
  }
} as const;

export type TranslationKey = keyof typeof translations.vi;

type I18nContextType = {
  lang: Language;
  setLang: (lang: Language) => void;
  t: (key: TranslationKey) => string;
};

const I18nContext = createContext<I18nContextType>({
  lang: "vi",
  setLang: () => {},
  t: (key: TranslationKey) => translations.vi[key] || key
});

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Language>("vi");

  useEffect(() => {
    const saved = localStorage.getItem("sentinellan_lang") as Language | null;
    if (saved === "vi" || saved === "en") {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setLangState(saved);
    }
  }, []);

  const setLang = (nextLang: Language) => {
    setLangState(nextLang);
    localStorage.setItem("sentinellan_lang", nextLang);
  };

  const t = (key: TranslationKey): string => {
    return translations[lang]?.[key] ?? translations.vi[key] ?? key;
  };

  return (
    <I18nContext.Provider value={{ lang, setLang, t }}>
      {children}
    </I18nContext.Provider>
  );
}

export function useTranslation() {
  return useContext(I18nContext);
}
