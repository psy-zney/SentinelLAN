# Hợp đồng API nâng cấp tháng 10 năm 2026

OpenAPI thực tế ở `/openapi/v1.json`. Tất cả HTTP route dưới đây có prefix `/api/v1`; cookie mutation của người dùng yêu cầu CSRF header. Admin chỉ thao tác trong OrganizationId đã xác thực. Agent dùng các header HMAC được mô tả trong [ADR 0029](../adr/0029-agent-request-signatures.md).

| Route | Quyền | Dữ liệu và kết quả |
|---|---|---|
| GET, PUT `/organization/monitoring-settings` | Admin | Settings + revision; PUT kèm reason, confirmed, nonce, expiresAt; trả revision mới |
| GET `/devices/{id}/observations`, `/devices/{id}/software` | Admin | Inventory/posture hoặc trạng thái phần mềm của máy trong tenant |
| GET `/my-device/observations`, `/my-device/software` | Employee | Máy được giao, server tự chọn; không nhận ID máy tùy ý |
| GET `/agent/software-rules` | Agent | Detection-only baseline của đúng tenant; MSI codes, source, rule revision |
| POST `/agent/heartbeat` | Agent | Inventory/security/software tùy chọn, cùng telemetry và idempotency key hiện có |
| POST `/agent/auth/upgrade` | Proof credential qua TLS | Device ID + secret cũ; trả khóa request chuyên dụng cho đến khi nhận request ký đầu tiên |
| GET `/software/prohibited-rules`, POST, PUT `/{id}` | Admin | UUID ProductCode/UpgradeCode, active, revision, reason/confirmed/nonce/expiresAt |
| GET `/departments` | Admin | Danh sách phòng ban, user/device membership, policy mặc định và revision |
| POST `/departments`, PUT `/{id}` | Admin | Name, policyId nullable, revision, confirmation |
| PUT `/departments/users/{id}`, `/departments/devices/{id}` | Admin | DepartmentId nullable, revision phòng ban và confirmation |
| POST `/commands/bulk` | Admin | DeviceIds 1–100, type RefreshPolicy/CollectTelemetryNow và confirmation; trả outcome/commandId từng máy |
| POST `/enrollment-packages` | Admin | Count 1–50, platform Windows/Linux và confirmation; trả connectionCode, expiresAt, filename, script từng máy; no-store |
| WebSocket `/hubs/agent-wake` (không có prefix API) | Agent HMAC | Chỉ tín hiệu `wake-up`, không có payload lệnh; group từ identity server |

`confirmation` là `{reason, confirmed, nonce, expiresAt}`; nonce UUID mới cho một thao tác, hạn tối đa năm phút. Một HTTP retry phải giữ nguyên payload/nonce; thao tác mới sau xung đột cần tải lại revision. Các endpoint mới trả 400 khi dữ liệu không hợp lệ, 403 khi sai quyền, 404 cho tài nguyên không thuộc scope và 409 cho replay/revision xung đột. Gói cài chưa cấu hình và kho ảnh tạm lỗi trả 503.

Phần mềm Agent gửi `{ruleId, source, detection, installedVersion, capturedAt, ruleRevision}`. Detection chỉ Installed, NotFound, Unknown hoặc NotSupported; tối đa 256 mục, không trùng rule/source. Mẫu của tenant khác bị từ chối; mẫu đúng tenant nhưng revision cũ/quy tắc vừa nghỉ được bỏ qua để heartbeat offline vẫn tới được server. Danh sách ảnh vẫn chỉ đọc metadata/Size, không đọc Content hoặc tải object.
