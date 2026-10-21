# Tổng quan SentinelLAN

Đối chiếu mã nguồn tháng 10/2026.

SentinelLAN quản lý các thiết bị đầu cuối được tổ chức cho phép trên LAN. Agent gửi telemetry kỹ thuật tối thiểu: trạng thái online, CPU, RAM, đĩa, phiên bản OS/Agent. Hai vai trò người dùng hiện hành là **Admin** và **Employee**; Agent là danh tính máy. Ảnh lỗi do nhân viên chủ động chọn, xem trước và đồng ý gửi; không chụp màn hình ngầm, ghi phím, đọc tệp cá nhân hay bắt lưu lượng mạng.

## Phạm vi và luồng chính

Admin cấp tài khoản/token enrollment một lần → Agent đăng ký trên máy được ủy quyền → gửi heartbeat có retry → Admin gán máy → Employee xem máy và báo sự cố → Admin xử lý trong tenant → audit ghi hành động. QR xác minh tem thiết bị; alert tự động theo [quy tắc giám sát](architecture/device-health-monitoring.md).

Migration `20261009065038_RetireLegacyRoles` khóa tài khoản có role ngoài Admin/Employee, đổi security stamp và thu hồi refresh session/token kích hoạt. `Technician`, `PlatformOwner` không có quyền hiện hành. Migration không tự cấp role thay thế hoặc tự mở khóa khi rollback.

**Giám sát host tùy chọn:** timer systemd chạy collector chỉ đọc tại host, ghi JSON cục bộ. API đọc file qua mount chỉ đọc; chỉ Admin thuộc đơn vị vận hành đã cấu hình được xem `/api/v1/host/status`. API không giữ SSH key, kết nối SSH, restart dịch vụ hay reboot VPS.

**Ngoài phạm vi:** rà quét IP, IDS/IPS, remote desktop, lệnh tùy ý và khóa/cô lập máy production. Mobile là giao diện Employee, không phải MDM quản lý điện thoại.

## Thành phần

| Thành phần | Vai trò |
|---|---|
| `apps/backend` | ASP.NET Core API, Application, Domain và Infrastructure/EF Core |
| `apps/agent` | Worker telemetry, bảo vệ credential, xác minh lệnh bằng public key |
| `apps/company` | Dashboard Admin Next.js, gọi typed API client |
| `apps/employee`, `apps/mobile` | Giao diện Employee; mobile cần nghiệm thu native riêng |
| PostgreSQL | Dữ liệu tenant, thiết bị, phiên, command và audit |
| Collector host | Số liệu Linux/systemd/Docker, xuất snapshot JSON cục bộ |

Domain không phụ thuộc Infrastructure; Application sở hữu use case/contract/quyền; Infrastructure triển khai port; API là composition root. Module trao đổi qua Application contract/event. Tenant scope có kiểm tra tường minh và EF global query filter, chưa có database row-level security.

## Kiểm soát và nghiệm thu

1. Token enrollment dùng một lần; server lưu hash token/credential. Sau thu hồi, credential cũ không gửi được heartbeat.
2. Heartbeat qua HTTPS giữ nguyên idempotency key khi retry. Production bảo vệ queue file tối đa 50 mục trong một giờ; Development mặc định dùng RAM.
3. Production ký lệnh bằng **RSA-PSS/SHA-256, khóa tối thiểu 3072 bit**, envelope `R2.<keyId>.<signature>`. Private key chỉ ở server; Agent nhận public key và key ID tương ứng. HMAC chỉ còn fallback Development khi chưa cấu hình RSA.
4. Admin tạo command với lý do, xác nhận, hạn và nonce. Agent kiểm tra chữ ký, máy đích, allow-list, hạn và nonce. Delivery lease/biên lai idempotent không bảo đảm thực thi đúng một lần qua mọi sự cố.
5. `SimulateLock`/`SimulateNetworkIsolation` luôn mô phỏng. Adapter Windows có hành động thật theo allow-list; khóa/cô lập thật cần lab flag và device ID được ủy quyền. Xem [nghiệm thu Agent](guides/real-agent-execution.md).
6. Forwarded headers chạy trước authentication/rate limit, chỉ tin proxy/IP network khai báo. Danh sách ảnh chỉ Select metadata và kích thước ảnh gốc; tải nội dung là endpoint riêng có kiểm tra tenant/request.
7. Nghiệm thu phải chứng minh Employee chỉ xem máy/yêu cầu của mình, tenant khác không đọc/sửa được dữ liệu, lệnh sai chữ ký/hạn/nonce bị từ chối và snapshot cũ được báo rõ.

## Tính năng nâng cấp ngày 21 tháng 10 năm 2026

Agent tự báo inventory Windows/Linux, posture bảo vệ Windows và đối chiếu MSI đã cấu hình; trang thiết bị và máy của tôi hiển thị dữ liệu cùng trạng thái chưa xác định/cũ. Admin cấu hình ngưỡng theo tổ chức, nhận cảnh báo và thông báo từ outbox; preventive CMMS tạo phiếu theo ngày khi sắp hết bảo hành hoặc đến chu kỳ.

Phòng ban liên kết người dùng và thiết bị, cung cấp policy mặc định; lệnh cập nhật chính sách/thông tin có thể gửi cho tối đa 100 máy. Gói đăng ký QR/script cấp riêng từng máy trong lô 1–50, hạn 15 phút, ghim server/public key/hash/publisher. Request Agent dùng HMAC S1 riêng từng máy; lệnh server vẫn ký RSA-PSS. SignalR chỉ đánh thức, HTTP poll/lease giữ cơ chế giao nhận. Có Redis backplane và S3/MinIO tùy chọn; Linux có adapter systemd/loginctl/nftables với quyền và rollback được kiểm tra. Xem [cấu hình, hợp đồng và giới hạn](guides/product-upgrade.md).

## Giới hạn cần báo cáo

Audit chặn sửa/xóa qua EF, chưa có WORM độc lập. Bảo vệ private key ký lệnh, khóa mã hóa và khóa xác thực riêng biệt; phân phối public key trước khi xoay khóa. Snapshot không chứng minh host chưa bị xâm nhập. Test InMemory không xác nhận PostgreSQL/TLS, native mobile hoặc hành động Windows thật. Nghiệm thu cần chạy trên PostgreSQL và thiết bị thử được ủy quyền; không dùng số test/báo cáo triển khai cũ làm trạng thái hiện tại.

Bản thảo khóa luận và dữ liệu tạm nằm ngoài bản nộp mã nguồn; `scratch/` được Git bỏ qua.

## Tài liệu liên quan

- [Kiến trúc](architecture/context.md), [thành phần](architecture/components.md), [giao lệnh](architecture/command-delivery.md).
- [Cài đặt](../README.md), [VPS](deployment/saas-vps-guide.md), [giám sát host](deployment/vps-operations.md), [enrollment](guides/agent-enrollment-guide.md).
- [Use cases](use-cases/use-cases.md), [demo](demo/demo-script.md), [từ điển dữ liệu](database/data-dictionary.md).
- [Quyền riêng tư](security/privacy.md), [threat model](security/threat-model.md), [ứng phó](security/incident-workflow.md).
