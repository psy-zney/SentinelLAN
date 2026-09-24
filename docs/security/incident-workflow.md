# Ứng phó sự cố SentinelLAN

Runbook này áp dụng cho sự cố thiết bị, nghi ngờ chiếm tài khoản/Agent, rò rỉ khóa, truy cập sai tenant hoặc mất tính sẵn sàng. Người trực ca ghi UTC time, người quyết định, tenant, phạm vi ảnh hưởng, correlation ID và hành động vào hệ thống ticket ngoài SentinelLAN; audit trong database chỉ là một nguồn bằng chứng, không phải kho WORM độc lập. Không ghi mật khẩu, token, khóa SSH hoặc nội dung cá nhân vào ticket/audit.

| Mức | Ví dụ | Hành động đầu tiên |
|---|---|---|
| P0 | Rò khóa ký/vault, vượt ranh giới tenant, truy cập Admin trái phép | Báo người phụ trách an ninh ngay; giữ bằng chứng, giới hạn ingress và dừng luồng liên quan |
| P1 | Agent bị chiếm, nhiều thiết bị mất kết nối, DB lỗi/backup không dùng được | Khoanh tenant/thiết bị, thu hồi credential, chuyển sang quy trình khôi phục |
| P2 | Một alert/incident đơn lẻ không có bằng chứng xâm nhập | Tạo ticket, xác minh, phân Technician theo mức độ và hạn xử lý |

1. **Xác minh:** kiểm tra `health/live`, `health/ready`, log reverse proxy/API, audit theo actor/device và giờ UTC. Phân biệt sự cố kết nối với hoạt động trái phép. Ghi hash của bản sao log và quyền truy cập bằng chứng.
2. **Khoanh vùng:** chặn nguồn độc hại ở proxy/firewall, giới hạn tài khoản hoặc thiết bị liên quan. Admin có thể thu hồi thiết bị trong dashboard; API sẽ từ chối credential Agent đó. Chỉ cô lập mạng/khóa máy sau phê duyệt vận hành phù hợp và công cụ được ủy quyền bên ngoài SentinelLAN; lệnh `Simulate*` của repo không làm việc này.
3. **Đối phó theo loại bí mật:** nếu lộ token enrollment thì để hết hạn hoặc kiểm tra và xử lý trong DB theo quy trình vận hành; token đã dùng không tái sử dụng được. Nếu lộ device secret, thu hồi thiết bị và đăng ký lại bằng token mới. Nếu lộ mật khẩu Admin, chặn đường truy cập và đổi mật khẩu theo quy trình quản trị; hiện chưa có API Admin tự thu hồi mọi phiên người dùng, nên có thể cần thao tác DB có kiểm soát. Nếu lộ khóa access-token, xoay khóa và dự kiến mọi access token hiện có mất hiệu lực. Nếu lộ khóa HMAC lệnh, cập nhật server và các Agent cùng đợt; để lệnh Pending cũ hết hạn và phát lại sau xác minh. Khóa vault mã hóa SSH key chưa có công cụ re-encrypt; không thay khóa đơn lẻ trước khi có bản sao/diễn tập di chuyển ciphertext.
4. **Khôi phục:** vá nguyên nhân, kiểm tra tenant scope, cấu hình TLS/CORS/CSRF, khởi động lại dịch vụ cần thiết, xác nhận `health/ready`, đăng nhập Admin/Employee bằng tài khoản thử được phép, Agent heartbeat và audit. Khôi phục DB từ dump chỉ sau khi diễn tập trên database riêng; đối chiếu timestamp và chấp nhận RPO/RTO của tổ chức. Không ghi đè production chỉ để “thử”.
5. **Đóng và học:** giữ bằng chứng theo chính sách lưu trữ, ghi quyết định và khoảng mất dữ liệu, cập nhật quy tắc cảnh báo/test. Nhắc lại quyền truy cập tối thiểu và phân phối khóa qua kênh an toàn.

Backup tạo bằng `scripts/backup-postgres.sh`; `scripts/restore-drill-postgres.sh` tạo database tạm để thử. Cần bảo vệ riêng `SERVER_VAULT_KEY`, cert TLS và backup. [OWASP Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html) là tham chiếu cho thu hồi/đổi khóa khi lộ bí mật; quy trình tại đây phải được điều chỉnh theo trách nhiệm pháp lý và hạ tầng thật.
