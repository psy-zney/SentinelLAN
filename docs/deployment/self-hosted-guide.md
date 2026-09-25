# Triển khai SentinelLAN trong LAN

Có hai chế độ cần tách rõ:

- **Development/lab:** `docker compose up --build -d` ở gốc repo, dùng `compose.yaml` và `compose.override.yaml`. API seed tổ chức `demo` để thử. HTTP và mật khẩu mẫu chỉ dùng trên máy/lab được ủy quyền, không công bố cho toàn LAN hoặc Internet.
- **Vận hành thật trong LAN:** dùng stack Production trong `deploy/vps/` với tên DNS nội bộ, chứng chỉ TLS do CA của tổ chức cấp và tin cậy trên trình duyệt/Agent. Làm theo [README](../../README.md) và [hướng dẫn Production](saas-vps-guide.md), thay DNS công khai bằng split DNS nội bộ. API sẽ không seed demo; tổ chức/Admin được bootstrap từ biến môi trường.

Trên máy chủ, kiểm tra tài nguyên, Docker Compose, DNS, chứng chỉ, đồng hồ UTC/NTP, dung lượng đĩa và đường truyền Agent → HTTPS 443. Chỉ reverse proxy được mở cho thiết bị khách; PostgreSQL giữ trong mạng container. `health/live` kiểm tra tiến trình, `health/ready` kiểm tra kết nối database. Sau deploy, thực hiện một vòng đăng ký Agent, gán Employee, xem `/my-device`, tạo incident thử và thu hồi trên thiết bị thử.

Backup bằng `scripts/backup-postgres.sh` và diễn tập bằng `scripts/restore-drill-postgres.sh`. Đưa dump sang lưu trữ riêng được mã hóa và lưu khóa vault ở nơi độc lập; áp lịch backup/retention phù hợp dữ liệu tổ chức. Production Agent giữ tối đa 50 mục telemetry trong file được bảo vệ, tối đa một giờ; Development mặc định dùng RAM. File queue không thay cho backup và cần kiểm thử khôi phục trên host triển khai. Quy trình sự cố nằm ở [incident-workflow](../security/incident-workflow.md).
