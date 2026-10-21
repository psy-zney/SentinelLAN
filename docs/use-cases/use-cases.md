# Use cases và ranh giới hành vi

Đối chiếu mã nguồn 21/10/2026. Admin và Employee là hai role người dùng đang được chấp nhận; Agent có scheme riêng. Technician/PlatformOwner đã nghỉ và bị khóa qua migration, không phải role có thể đăng nhập hiện tại.

| Vai trò | Luồng đang hỗ trợ |
|---|---|
| Admin | Quản lý người dùng/thiết bị/phòng ban, cấp mã một lần hoặc lô QR/script, policy mặc định, bulk hai lệnh an toàn, ngưỡng cảnh báo/bảo trì, catalog/quy tắc MSI, hỗ trợ/WorkOrder/audit; xem host snapshot khi đúng đơn vị vận hành |
| Employee | Máy được giao, inventory/posture/phần mềm được công bố, yêu cầu hỗ trợ và ảnh chủ động gửi, thông báo, phản hồi và mã bảo trì đã duyệt |
| Agent | Enrollment/recovery, request ký HMAC, heartbeat tối thiểu, baseline phần mềm đã cấu hình, poll/lease/xác minh RSA, biên lai và SignalR wake-up |

## Kịch bản cần kiểm tra

1. Admin cấp mã riêng từng máy, Agent đăng ký có inventory, Admin gán máy/phòng ban. Employee chỉ xem máy của mình; tenant khác không đọc/sửa được. Token dùng lại/hết hạn và credential thu hồi bị từ chối.
2. HMAC sai method/path/body/device, timestamp quá hạn, nonce lặp và downgrade secret sau xác nhận đều bị từ chối. RSA command sai chữ ký/hạn/máy/nonce không thực hiện; receipt idempotent.
3. Chỉnh CPU xuống 20%, duy trì 0 với mẫu mới đủ ngưỡng: alert, outbox và thông báo Admin xuất hiện. Unknown không tự đóng cảnh báo bảo vệ/phần mềm. Preventive chỉ tạo một phiếu khi đến kỳ/sắp hết bảo hành.
4. Policy máy ưu tiên policy phòng ban; mặc định mới xếp RefreshPolicy. Bulk chỉ RefreshPolicy/CollectTelemetryNow, tối đa 100 máy và có kết quả từng máy. Gói enrollment chứa token khác nhau cho lô tối đa 50 máy.
5. Simulate luôn mô phỏng; RestartService Windows/Linux chỉ dùng allow-list và quyền. Lock/isolation cần lab/device opt-in; Linux phải xác nhận rollback timer trước apply nftables. Không có API shell tùy ý hay SSH/reboot VPS.
6. Danh sách ảnh chỉ metadata; ảnh S3 vẫn xác thực tenant/request và AAD khi tải. Redis/MinIO, TLS, restore và hành động OS thật cần nghiệm thu riêng.

Xem [demo](../demo/demo-script.md), [hướng dẫn nâng cấp](../guides/product-upgrade.md), [giao lệnh](../architecture/command-delivery.md) và [threat model](../security/threat-model.md).
