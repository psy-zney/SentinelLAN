from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]

def update(relative, changes):
    path = root / relative
    text = path.read_text(encoding="utf-8")
    for old, new in changes.items():
        if old not in text:
            raise ValueError(f"Missing text in {relative}: {old}")
        text = text.replace(old, new)
    path.write_text(text, encoding="utf-8", newline="\n")

for app in ("company", "employee"):
    (root / f"apps/{app}/src/app/globals.css").write_text('@import "tailwindcss";\n@import "../../../../packages/web-ui/src/styles/app.css";\n', encoding="utf-8")

path = root / "packages/web-ui/src/lib/i18n.tsx"
text = path.read_text(encoding="utf-8")
vi, en = text.split("  en: {", 1)
labels = {
    "appTagline": "Quản lý máy tính và hỗ trợ nhân viên",
    "dashboard": "Tổng quan",
    "policies": "Cài đặt bảo vệ",
    "commands": "Thao tác trên máy",
    "alerts": "Cảnh báo",
    "auditLogs": "Lịch sử hoạt động",
    "users": "Tài khoản",
    "myDevice": "Máy tính của tôi",
    "loading": "Đang tải…",
    "cancel": "Hủy",
    "error": "Chưa tải được dữ liệu. Hãy thử lại.",
    "online": "Đang kết nối",
    "offline": "Chưa kết nối",
    "delivered": "Đã gửi đến máy",
    "open": "Chưa xử lý",
    "subtitleDashboard": "Tình trạng thiết bị và các cảnh báo cần xử lý.",
    "subtitlePolicies": "Lưu cấu hình bảo vệ cho thiết bị. Các thiết lập khóa màn hình và USB chưa được áp dụng lên máy.",
    "subtitleCommands": "Gửi yêu cầu đến thiết bị và xem kết quả. Thao tác có nhãn “Mô phỏng” chỉ dùng để thử nghiệm.",
    "subtitleAlerts": "Các cảnh báo do IT ghi nhận để theo dõi và xử lý.",
    "subtitleAudit": "Xem ai đã thực hiện thao tác, lý do và kết quả.",
    "subtitleUsers": "Tạo tài khoản, gửi lời mời và quản lý quyền của từng người trong công ty.",
    "subtitleMyDevice": "Thông tin máy tính được công ty giao cho bạn.",
    "totalDevices": "Tổng thiết bị",
    "onlineDevices": "Đang kết nối",
    "offlineDevices": "Chưa kết nối",
    "openAlerts": "Cần xử lý",
    "managedDevices": "Thiết bị trong công ty",
    "agentVersion": "Phiên bản SentinelLAN",
    "lastSeen": "Kết nối gần nhất",
    "telemetryHistory": "Thông số máy tính",
    "cpuUsage": "Bộ xử lý (CPU)",
    "ramUsage": "Bộ nhớ (RAM)",
    "diskUsage": "Ổ đĩa",
    "noTelemetry": "Máy chưa gửi thông số về hệ thống.",
    "policyList": "Cấu hình bảo vệ",
    "createPolicyBtn": "Thêm cấu hình",
    "policyNameLabel": "Tên cấu hình",
    "idleTimeoutLabel": "Khóa máy sau khi không sử dụng (phút)",
    "assignedCountLabel": "Thiết bị được gán",
    "assignPolicyBtn": "Chọn thiết bị",
    "createPolicyModalTitle": "Thêm cấu hình bảo vệ",
    "assignPolicyModalTitle": "Chọn thiết bị dùng cấu hình",
    "selectDevicePrompt": "Chọn thiết bị",
    "policyCreatedSuccess": "Đã lưu cấu hình.",
    "policyAssignedSuccess": "Đã gán cấu hình cho thiết bị.",
    "commandHistory": "Các thao tác đã gửi",
    "dispatchCommandBtn": "Thêm thao tác",
    "commandModalTitle": "Gửi thao tác đến thiết bị",
    "commandTypeLabel": "Việc cần thực hiện",
    "commandReasonLabel": "Lý do",
    "commandConfirmCheck": "Tôi được phép thực hiện thao tác này trên thiết bị đã chọn.",
    "commandSignAndDispatch": "Gửi thao tác",
    "commandDispatchedSuccess": "Đã gửi yêu cầu. Xem kết quả để biết máy đã thực hiện hay chưa.",
    "dispatching": "Đang gửi…",
    "commandTarget": "Thiết bị",
    "commandType": "Thao tác",
    "commandIssuedBy": "Người gửi",
    "commandIssuedAt": "Thời gian gửi",
    "alertSummary": "Cảnh báo cần xử lý",
    "triggerAlertBtn": "Ghi nhận cảnh báo",
    "triggerAlertModalTitle": "Ghi nhận cảnh báo",
    "alertSeverityLabel": "Mức độ",
    "alertTriggerSubmit": "Lưu cảnh báo",
    "alertTriggerSuccess": "Đã ghi nhận cảnh báo.",
    "ackBtn": "Tiếp nhận",
    "resolveBtn": "Đã xử lý",
    "auditTrail": "Lịch sử hoạt động",
    "noAuditLogs": "Chưa có hoạt động nào được ghi nhận.",
    "orgDetails": "Thông tin công ty",
    "orgCode": "Mã công ty",
    "orgName": "Tên công ty",
    "userDirectory": "Tài khoản trong công ty",
    "rbacTitle": "Quyền của từng vai trò",
    "myDeviceHeader": "Máy tính được giao cho bạn",
    "transparencyBadge": "Thông tin theo dõi trên máy",
    "appliedPolicy": "Cấu hình được giao (chưa áp dụng)",
    "recentActionsTitle": "IT đã làm gì trên máy của bạn",
    "signInSubtitle": "Quản lý thiết bị và hỗ trợ nhân viên.",
    "orgCodePrompt": "Mã công ty",
    "signingIn": "Đang đăng nhập…",
    "loginError": "Mã công ty, email hoặc mật khẩu chưa đúng.",
    "vpsNodes": "Máy chủ",
    "subtitleVps": "Theo dõi máy chủ Linux qua kết nối SSH.",
    "totalVpsNodes": "Tổng máy chủ",
    "onlineVpsNodes": "Đang kết nối",
    "addVpsNodeBtn": "Thêm máy chủ",
    "vpsNameLabel": "Tên máy chủ",
    "vpsHostLabel": "Địa chỉ IP hoặc tên miền",
    "vpsPrivateKeyLabel": "Khóa riêng SSH",
    "vpsVaultNotice": "Khóa SSH được lưu mã hóa và chỉ dùng để kết nối máy chủ.",
    "restartVpsServiceBtn": "Khởi động lại dịch vụ",
    "vpsServiceRestartTitle": "Khởi động lại dịch vụ",
    "vpsServiceNameLabel": "Dịch vụ",
    "vpsServiceReasonLabel": "Lý do",
    "vpsNoNodes": "Chưa có máy chủ. Chọn “Thêm máy chủ” để bắt đầu.",
    "tabOverview": "Thông tin thiết bị",
    "tabTimeline": "Lịch sử sử dụng",
    "tabCmms": "Sửa chữa & bảo trì",
    "tabTco": "Chi phí",
    "assetInfo": "Thông tin thiết bị",
    "serialNumber": "Số sê-ri",
    "model": "Dòng máy",
    "location": "Vị trí",
    "purchaseCost": "Giá mua",
    "tcoAnalysis": "Chi phí sửa chữa và thay mới",
    "cumulativeMaintenance": "Tổng chi phí bảo trì",
    "recommendation": "Đề xuất",
    "editAssetProfile": "Sửa thông tin thiết bị",
    "quickCommand": "Thao tác trên máy",
    "fieldPortal": "Thông tin qua mã QR",
    "activationSubtitle": "Đặt mật khẩu để bắt đầu sử dụng.",
    "activationFailed": "Chưa kích hoạt được. Liên kết có thể đã dùng hoặc hết hạn.",
    "scanTitle": "Quét mã QR thiết bị",
    "scanSubtitle": "Quét tem trên máy tính để xem thông tin thiết bị.",
    "submitCode": "Mở thông tin thiết bị",
    "qrLabelTitle": "Tem QR thiết bị",
    "generateQr": "Tạo tem QR",
    "rotateQr": "Thay tem QR",
    "qrRotateReason": "Lý do thay tem",
    "publicAssetTag": "Thông tin trên tem thiết bị",
    "createPendingUser": "Mời người dùng",
    "activationLinkTitle": "Liên kết kích hoạt tài khoản",
    "activationLinkWarning": "Sao chép và gửi riêng cho người được mời. Liên kết dùng một lần, hết hạn sau 24 giờ và chỉ hiển thị ở đây một lần.",
    "myDeviceTitle": "Máy tính của tôi",
    "privacyCommitment": "Công ty thu thập thông tin gì?",
    "privacyCollected": "Thông tin được thu thập",
    "privacyProhibited": "Thông tin không được thu thập",
    "transparencyManifest": "Thông tin theo dõi trên máy",
    "dataRetention": "Thời gian lưu",
    "reportMyDeviceIssue": "Nhờ IT hỗ trợ",
    "issueSubmitted": "Đã gửi yêu cầu cho IT.",
    "recentIncidents": "Yêu cầu đã gửi",
}
for key, value in labels.items():
    vi, count = re.subn(rf'(    {key}: )"[^"\n]*"', lambda m: m[1] + '"' + value + '"', vi)
    if count != 1:
        raise ValueError(f"Translation {key}: {count}")
path.write_text(vi + "  en: {" + en, encoding="utf-8", newline="\n")

# Remove decorative emoji from existing UI copy; keep meaningful status marks.
decorative = re.compile('[\U0001F300-\U0001FAFF\u26a0\u26a1\u23f8\u25b6\u2795\ufe0f] ?')
for folder in ("packages/web-ui/src/components", "apps/platform/src/app"):
    for path in (root / folder).rglob("*.tsx"):
        text = path.read_text(encoding="utf-8")
        path.write_text(decorative.sub("", text), encoding="utf-8", newline="\n")

for app, title, description in (
    ("company", "SentinelLAN · Quản trị công ty", "Quản lý thiết bị và hỗ trợ nhân viên."),
    ("employee", "SentinelLAN · Nhân viên", "Nhờ IT hỗ trợ và xem thông tin máy tính của bạn."),
):
    update(f"apps/{app}/src/app/layout.tsx", {
        "SentinelLAN — Quản lý thiết bị đầu cuối được ủy quyền": title,
        "Đăng ký Agent, giám sát telemetry kỹ thuật và xử lý sự cố cho thiết bị được tổ chức cho phép": description,
    })

update("packages/web-ui/src/lib/portal.ts", {"Cổng nhân viên": "Nhân viên", "Quản trị công ty · IT": "Quản trị công ty"})
update("packages/web-ui/src/components/support-center-view.tsx", {
    "Hàng đợi hỗ trợ": "Yêu cầu hỗ trợ",
    "Đang kết nối với IT…": "Đang tải yêu cầu…",
    "Chưa có yêu cầu. Khi gặp vấn đề, chọn “Tôi gặp sự cố”.": "Chưa có yêu cầu trong danh sách này.",
    "Trao đổi với IT</h2><p>Chọn một yêu cầu": '{employee ? "Trao đổi với IT" : "Chi tiết yêu cầu"}</h2><p>Chọn một yêu cầu',
})

update("packages/web-ui/src/components/device-table.tsx", {
    "Xem danh mục chi tiết →": "Xem tất cả",
    '"Phân công"': '"Người sử dụng"',
    '"Lệnh" : "Cmd"': '"Thao tác" : "Action"',
    '"Hỏng" : "Issue"': '"Báo hỏng" : "Issue"',
    "Ký số & Gửi lệnh": "Gửi thao tác",
})
