# SentinelLAN công ty tự quản lý

Mỗi bản triển khai phục vụ một công ty nội bộ. Đăng nhập web/mobile chỉ cần email và mật khẩu; không nhập mã công ty. Máy chủ cố định công ty bằng `SENTINELLAN_LOGIN_ORG_CODE`, mặc định lấy `BOOTSTRAP_ORG_CODE`. Mã là cấu hình của IT, không phải lựa chọn của người đăng nhập. Khi đổi mã công ty trong dữ liệu, phải đồng bộ cấu hình đăng nhập và `SENTINELLAN_HOST_MONITOR_ORG_CODE` nếu có dùng theo dõi VPS.

Chạy từ thư mục gốc với `docker compose --env-file deploy/lan/.env -f deploy/lan/compose.yaml up --build -d`.

## Chuẩn bị

- Máy chủ Linux/VM với Docker Compose, địa chỉ LAN ổn định và DNS nội bộ.
- `PUBLIC_DOMAIN` là tên DNS nội bộ mà cả hai laptop phân giải được, không kèm scheme hoặc path.
- Đặt chứng chỉ có SAN đúng tên DNS vào `deploy/lan/certs/fullchain.pem` và khóa riêng vào `deploy/lan/certs/privkey.pem`. Cài CA tin cậy trên các laptop; không bỏ xác minh TLS.
- Tạo `.env` riêng với POSTGRES_PASSWORD, BOOTSTRAP_ORG_CODE, BOOTSTRAP_ORG_NAME, BOOTSTRAP_ADMIN_EMAIL, BOOTSTRAP_ADMIN_PASSWORD, COMMAND_PRIVATE_KEY_PEM, COMMAND_KEY_ID, ACCESS_TOKEN_SIGNING_KEY, SERVER_VAULT_KEY, PUBLIC_DOMAIN. Mật khẩu Admin ít nhất 16 ký tự; khóa access token và vault ít nhất 32 ký tự, ngẫu nhiên và khác nhau. Khóa ký lệnh là RSA tối thiểu 3072 bit, PKCS8 PEM; không dùng giá trị demo. PEM nhiều dòng có thể đặt trong giá trị được nháy đơn của env file; không in cấu hình ra màn hình/log.
- Chỉ mở 443 cho mạng công ty và 80 cho chuyển hướng. PostgreSQL/API không công bố cổng host.

`SENTINELLAN_DEPLOYMENT_MODE=SelfHost` tắt toàn bộ API Platform và dịch vụ VPS; không cần biến PLATFORM_OWNER. Các portal `/company` và `/employee` vẫn hoạt động; `/platform` trả 404. Bootstrap công ty/Admin chạy khi database trống. Không dùng database Platform cũ để giả định đã chuyển sang một công ty; tạo database mới hoặc lập kế hoạch di chuyển được kiểm tra riêng.

## Nghiệm thu F01/F02

1. Kiểm tra `docker compose --env-file deploy/lan/.env -f deploy/lan/compose.yaml config --quiet` rồi khởi động. Không in cấu hình đã render vì có secret.
2. Từ hai laptop mở HTTPS đúng tên DNS, đăng nhập Admin công ty, tạo người dùng và đăng xuất.
3. Xác nhận `/api/v1/platform/auth/login` và `/platform` trả 404; không có người dùng Platform Owner trong database mới.
4. Khởi động lại dịch vụ; xác nhận bootstrap không tạo thêm công ty/Admin hoặc đổi mật khẩu.
5. Ngắt đường Internet ngoài nhưng giữ LAN/DNS; đăng nhập và đọc danh sách thiết bị còn hoạt động. Chụp bằng chứng thời gian và cấu hình lab.
6. Sao lưu trước nâng cấp. Kiểm tra phục hồi ở database độc lập trước khi công bố đạt F17.

Build lần đầu và tải image cần Internet hoặc cache/image đã chuẩn bị. Chạy offline được nghiệm thu sau khi đã build, có DNS/chứng chỉ tin cậy và dữ liệu cục bộ. SelfHost bắt buộc RSA; G2/G3 vẫn cần nghiệm thu Windows trước khi phân phối Agent rộng.

## Bộ cài công ty

Build với `deploy/agent/build-msi.ps1 -CompanyServerUrl https://ten-dns-noi-bo -CommandPublicKey <base64-SPKI> -CommandKeyId <key-id>`. Chỉ đưa khóa công khai vào bộ cài; server giữ khóa riêng tương ứng. Token cấp trong portal là chuỗi ngẫu nhiên 64 ký tự không chứa địa chỉ/thời hạn; server kiểm tra hạn và việc sử dụng. Bộ cài công ty từ chối SL1 và không cho người nhập token đổi máy chủ. Xác minh nguồn/checksum của bộ cài qua IT trước khi cài; checksum đơn lẻ không thay xác thực nhà phát hành.
