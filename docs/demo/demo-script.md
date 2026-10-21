# Kịch bản demo và checklist nghiệm thu

Dùng môi trường Development/lab và máy được ủy quyền. Ghi rõ PostgreSQL hoặc InMemory, phiên bản Agent/API, RSA key ID và các adapter thực sự được chạy. Không chiếu secret, token, QR enrollment còn hạn hoặc thông tin cá nhân.

## Luồng trình diễn

1. Admin đăng nhập, xem inventory/posture và thời gian đo. Agent mới enrollment tự điền serial/CPU/RAM/đĩa/IP LAN; thiếu quyền là chưa xác định, không báo an toàn giả.
2. Cấp lô hai gói cài 15 phút, chứng minh mã khác nhau và dùng lại bị từ chối. QR tạo trong browser; không chạy script cài trên máy làm việc. Gói Windows xác minh hash/publisher; Linux xác minh hash.
3. Tạo phòng ban, gán policy và máy/nhân viên riêng. Policy gán riêng máy được ưu tiên; xếp RefreshPolicy hoặc chọn hai máy gửi CollectTelemetryNow. Theo dõi kết quả từng máy và audit.
4. Khi có mẫu CPU mới ≥20%, đổi ngưỡng CPU thành 20 và duy trì 0, lưu với lý do/xác nhận. Theo dõi alert và thông báo IT; sau demo khôi phục ngưỡng. Mẫu cũ hoặc máy offline không chứng minh đường CPU hoạt động. Kiểm thử MonitoringNotificationTests là bằng chứng logic với fixture.
5. Xem posture AV/firewall/BitLocker và phần mềm MSI được cấu hình. Chỉ thử tắt bảo vệ hoặc cài phần mềm trên lab có console/khôi phục đã được phép; có thể dùng adapter fixture trong bài test và ghi rõ. Mẫu Unknown không đóng alert; phần mềm cấm không bị tự gỡ.
6. Thiết bị có mốc bảo trì quá sáu tháng hoặc bảo hành còn dưới 30 ngày sinh Preventive Scheduled. Chạy lại không tạo trùng; đây là phiếu cần người IT xử lý.
7. Gửi SimulateLock có lý do/xác nhận. Quan sát wake-up rồi poll, RSA, nonce và receipt; không khóa màn hình. Không cam kết dưới một giây nếu chưa đo độ trễ. Thử mất SignalR và xác nhận poll vẫn nhận lệnh.
8. Xem host JSON snapshot read-only, báo cũ đúng khi ngừng collector; không trình diễn SSH/reboot VPS từ API.
9. Xem danh sách ảnh chỉ metadata và tải đúng tenant. Redis/MinIO là demo riêng sau khi đã cấu hình TLS/credentials và kiểm tra phục hồi. Giữ mặc định DB/một node nếu chưa nghiệm thu dịch vụ ngoài.

## Checklist ghi kết quả

- [ ] Backup trước migration ProductUpgrade; PostgreSQL readiness, tenant FKs và restore đã được kiểm chứng.
- [ ] Khóa RSA/public key đúng; HMAC request sai body/nonce/time bị từ chối; không log secret.
- [ ] Employee/tenant khác bị chặn; role cũ bị khóa.
- [ ] Mẫu mới, posture Unknown, trạng thái phần mềm và revision được trình bày đúng.
- [ ] Chỉnh ngưỡng tạo alert và thông báo với mẫu mới; khôi phục cấu hình sau demo.
- [ ] Preventive, bulk, enrollment một lần và outbox retry không tạo bản sao nghiệp vụ.
- [ ] Kiểm tra native Windows/Linux/phone/Redis/S3 riêng; ghi chưa thử nếu chỉ có fake/InMemory.
- [ ] Audit có actor/tenant/device/reason/outcome; không coi EF append-only là WORM.

Bằng chứng hiện tại được ghi tại [trạng thái nâng cấp](../roadmap/product-upgrade-2026-10.md). Không dùng số test hoặc ảnh triển khai cũ làm bằng chứng bản mới.
