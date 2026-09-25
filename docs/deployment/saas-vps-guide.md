# Triển khai SentinelLAN trên VPS

Đọc [README](../../README.md) để xem luồng khởi tạo và kiểm thử. Stack Production ở `deploy/vps/docker-compose.prod.yaml`: PostgreSQL nội bộ, API, web và Nginx TLS. Redis đã bỏ vì ứng dụng hiện không dùng; command Pending và audit nằm trong PostgreSQL. Không có demo seed ở Production.

Nếu dùng `sentinellan-vps-deployment.tar.gz` từ GitHub Release, giải nén vào một thư mục riêng và giữ nguyên cấu trúc gốc `apps/`, `packages/`, `deploy/`, `scripts/`, `package-lock.json`. Chạy các lệnh bên dưới từ thư mục gốc đó; Compose cần mã nguồn và Dockerfile để build. Release Agent Windows/Linux là bản self-contained, không cần cài .NET runtime riêng trên máy đích.

## Chuẩn bị

- Một tên DNS trỏ tới VPS và chứng chỉ TLS tin cậy cho tên đó. Đặt `fullchain.pem`, `privkey.pem` tại `deploy/vps/certs/`; giữ private key chỉ root đọc. Kiểm tra chuỗi chứng chỉ và hạn hết hạn trước khi chuyển lưu lượng.
- Docker Engine, Compose v2, `openssl`, `curl`; mở cổng 443 và 80 (80 chỉ chuyển hướng). Không publish cổng PostgreSQL/API/web trực tiếp.
- Chuẩn bị mã tổ chức, tên tổ chức và email Admin thật. Script tạo mật khẩu/khóa ngẫu nhiên vào `deploy/vps/.env` mode 0600. Sao lưu file này vào kho bí mật, tránh chụp màn hình hoặc gửi chat.

```bash
export PUBLIC_DOMAIN=sentinel.example.com
export BOOTSTRAP_ORG_CODE=example
export BOOTSTRAP_ORG_NAME='Example Organization'
export BOOTSTRAP_ADMIN_EMAIL=admin@example.com
sudo --preserve-env=PUBLIC_DOMAIN,BOOTSTRAP_ORG_CODE,BOOTSTRAP_ORG_NAME,BOOTSTRAP_ADMIN_EMAIL bash deploy/vps/setup-vps.sh
```

Lần đầu, API migrate database và tạo đúng một tổ chức/Admin. Khi database đã có dữ liệu, bootstrap không tạo lại. Đổi các biến bootstrap trong `.env` sau đó không đổi tài khoản hiện có. API từ chối khóa thiếu/yếu và database có tài khoản demo với mật khẩu mặc định. Không đưa `.env` vào Git hay Docker image.

## Xác nhận kết nối

```bash
curl --fail https://sentinel.example.com/health/live
curl --fail https://sentinel.example.com/health/ready
docker compose --env-file deploy/vps/.env -f deploy/vps/docker-compose.prod.yaml ps
docker compose --env-file deploy/vps/.env -f deploy/vps/docker-compose.prod.yaml exec postgres pg_isready
```

Đăng nhập qua HTTPS, tạo Employee, cấp token và enroll một Agent được ủy quyền. Xác minh máy xuất hiện trong tenant, Employee chỉ xem máy đã gán, rồi thử thu hồi credential trên máy thử. Nếu `ready` lỗi, xem log API/PostgreSQL và kết nối DB; nếu TLS lỗi, dừng cấp token cho đến khi sửa DNS/chứng chỉ. OpenAPI không mở ngoài Development.

## Backup và cập nhật

```bash
bash scripts/backup-postgres.sh deploy/vps/docker-compose.prod.yaml deploy/vps/.env
bash scripts/restore-drill-postgres.sh backups/postgres/<file>.dump deploy/vps/docker-compose.prod.yaml deploy/vps/.env
docker compose --env-file deploy/vps/.env -f deploy/vps/docker-compose.prod.yaml up -d --build
```

Lưu dump mã hóa ở vị trí tách khỏi VPS, giữ riêng khóa vault/cert và ghi hạn lưu theo quy định. Thử restore trên database tạm trước cập nhật lớn. Script deploy không dừng toàn bộ stack trước khi cập nhật, nhưng migration có thể cần cửa sổ bảo trì và backup đã xác minh. Cấu hình reverse proxy/nginx dùng chứng chỉ đã cấp; không có bước xin chứng chỉ tự động. Khi nghi ngờ xâm nhập, dùng [runbook sự cố](../security/incident-workflow.md).
