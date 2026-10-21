# Theo dõi VPS trong Web Admin

Ngày 2026-10-09. Khôi phục giám sát VPS sau khi bỏ PlatformOwner. Chỉ khôi phục chức năng đọc trạng thái máy chủ; không đưa lại Platform API, quản trị nhiều công ty hay lệnh SSH.

Trang `/company/vps` gọi `GET /api/v1/host/status`. Policy Admin và use case cùng kiểm tra quyền. Chỉ Admin của organization có code khớp `SENTINELLAN_HOST_MONITOR_ORG_CODE` được đọc; cấu hình VPS lấy code từ `BOOTSTRAP_ORG_CODE`. Truy vấn identity đi qua Application contract `IAuthenticationStore`, không truy cập chéo bảng từ module giám sát.

Timer root hiện có chạy bộ thu thập cố định mỗi phút, ghi JSON atomically quyền 0640, nhóm API 1654. API chỉ mount thư mục snapshot read-only, không nhận Docker socket/SSH key/quyền root. Docker ps/stats giới hạn label Compose project `sentinellan-prod`; chỉ đọc telemetry, trạng thái và cổng, không đọc environment, command hoặc log của container. Cổng host cho phép xem listener TCP/UDP và tên process, không command arguments.

Snapshot quá ba phút hoặc hơn một phút trong tương lai được đánh dấu cũ. Snapshot lỗi/mất/quá 2 MiB không được coi là khỏe. API trả `private, no-store`; UI làm mới mỗi phút, cho phép làm mới thủ công, giữ dữ liệu cũ với nhãn khi lỗi mạng và xóa dữ liệu khi 401/403. Không đổi schema hay khóa mã hóa/chữ ký Agent.

Kiểm thử bao gồm anonymous/Employee/khác tenant, no-store/OpenAPI, dữ liệu cũ/tương lai, file lỗi/quá lớn/unknown fields, và luồng browser vào trang/làm mới/thu hồi quyền. Triển khai giữ image và release cũ để rollback; nâng API/Admin và sidecar/gateway trong riêng project SentinelLAN, không restart PostgreSQL hoặc dịch vụ dự án khác.
