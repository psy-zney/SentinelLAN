# Mô hình đe dọa SentinelLAN

SentinelLAN quản lý thiết bị được tổ chức cho phép. Tài sản cần bảo vệ gồm tài khoản Admin/Employee/Technician, credential Agent, SSH private key VPS, telemetry, phiếu sự cố và audit. Ranh giới tin cậy chính: trình duyệt ↔ API, Agent ↔ API, API ↔ PostgreSQL, API ↔ VPS qua SSH.

| Đe dọa | Kiểm soát đã có | Giới hạn cần vận hành |
|---|---|---|
| Đăng nhập giả / đánh cắp phiên | Mật khẩu băm, refresh token xoay vòng, cookie HttpOnly/SameSite/Secure ở Production; khóa tài khoản thu hồi refresh và vô hiệu access token hiện tại bằng security stamp | Bảo vệ TLS private key và secret server; điều tra đăng nhập bất thường |
| Agent giả / lệnh phát lại | Token đăng ký một lần, credential riêng từng thiết bị, chữ ký và hạn/nonce của lệnh, tenant và trạng thái thiết bị được kiểm tra | Thu hồi credential ngay khi mất máy; đồng bộ giờ hệ thống |
| Sửa dữ liệu / vượt tenant | Service kiểm tra OrganizationId của tài nguyên liên quan; API áp dụng authorization policy; tác vụ nhạy cảm lưu actor, lý do và kết quả trong audit | Audit hiện ở cùng PostgreSQL, chưa có WORM/off-site; phải sao lưu và hạn chế quyền DBA |
| Lộ dữ liệu kỹ thuật / QR | Chỉ thu thập telemetry kỹ thuật; mã QR và token chỉ lưu dạng hash, mã QR gốc chỉ trả một lần; trình quét từ chối URL ngoài origin | Người có tem QR vẫn thấy thông tin công khai tối thiểu; xoay/thu hồi khi tem bị chụp hoặc mất |
| SSH trung gian / lộ private key | SSH private key mã hóa trong DB bằng server vault key; mọi phiên SSH cần fingerprint SHA256 đã xác minh qua kênh tin cậy; node cũ thiếu pin không được kết nối | Người vận hành phải kiểm tra fingerprint qua console VPS, không tin kết quả `ssh-keyscan` đơn độc; bảo vệ vault key tách khỏi backup DB |
| Quá tải / mất mạng | Endpoint nhạy cảm có rate limit; heartbeat idempotent; Production Agent lưu tối đa 50 telemetry item trong file được bảo vệ, retry có exponential delay và jitter ±500 ms | Development queue chỉ ở memory; Production queue phụ thuộc khóa/permissions của host, chưa được kiểm chứng sau restart trên mọi OS; chưa có broker hoặc multi-node SignalR. Cần giám sát dung lượng, restore drill và ingress DDoS ở hạ tầng |
| Lạm dụng lệnh | Agent chỉ nhận allow-list; khóa/cô lập, restart service trên Agent và một số lệnh hiện mô phỏng; VPS restart qua SSH là thật và giới hạn tên service | Không biến mô phỏng thành thao tác OS khi chưa có kiểm soát lab/thiết bị được ủy quyền |

Việc giảm thu thập dữ liệu không tự động đáp ứng mọi luật bảo vệ dữ liệu. Đơn vị triển khai phải đặt chính sách lưu giữ, thông báo cho nhân viên và kiểm tra nghĩa vụ pháp lý riêng. Khi nghi ngờ xâm nhập, dùng [runbook ứng phó](incident-workflow.md).
