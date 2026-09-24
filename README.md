# SentinelLAN

SentinelLAN quản lý thiết bị đầu cuối được tổ chức cho phép: Admin cấp tài khoản và mã đăng ký, Agent gửi telemetry kỹ thuật, nhân viên xem máy được giao và báo sự cố, kỹ thuật viên xử lý cảnh báo/công việc. Dữ liệu của mỗi tổ chức được giới hạn bằng `OrganizationId`. Các lệnh khóa máy và cô lập mạng vẫn **chỉ mô phỏng**; không bật hành vi thay đổi hệ điều hành trong bản này.

## Cấu trúc dự án

| Đường dẫn | Trách nhiệm |
|---|---|
| `apps/backend/src/SentinelLAN.Domain` | Entity, trạng thái và quy tắc nghiệp vụ |
| `apps/backend/src/SentinelLAN.Application` | Use case, DTO, validation, phân quyền và port |
| `apps/backend/src/SentinelLAN.Infrastructure` | EF Core/PostgreSQL, kho dữ liệu, hash, mã hóa, SSH adapter |
| `apps/backend/src/SentinelLAN.Api` | HTTP, xác thực, rate limit, SignalR, composition root |
| `apps/agent/src` | Worker, thu thập telemetry và credential store của Agent |
| `apps/web` | Dashboard Next.js, typed API client, E2E |
| `apps/mobile` | Ứng dụng nhân viên Expo; chạy và kiểm tra riêng |
| `deploy` | Dockerfile, reverse proxy và bộ cài Agent |
| `scripts` | Khởi tạo, kiểm thử, sao lưu và diễn tập khôi phục |

Ranh giới kiến trúc và luồng chi tiết nằm trong [kế hoạch MVP](MVP_IMPLEMENTATION_PLAN.md) và [ADR](docs/adr/0002-modular-monolith.md). Tài liệu trong `docs/local/`, `docs/private/`, chứng chỉ, dump, `.env` và ghi chú hạ tầng cá nhân không được đưa vào Git hoặc Docker build context.

## Chạy cục bộ bằng Docker Compose

Cần Docker Engine/Desktop và Compose v2. Đây là cấu hình **Development**; nó tạo dữ liệu `demo` để thử luồng, không dùng để công bố Internet.

```bash
git clone https://github.com/psy-zney/SentinelLAN.git
cd SentinelLAN
cp .env.example .env
docker compose up --build -d
docker compose ps
curl --fail http://localhost:8080/health/live
curl --fail http://localhost:8080/health/ready
```

Trên PowerShell, thay `cp` bằng `Copy-Item .env.example .env` và dùng `Invoke-WebRequest http://localhost:8080/health/ready`. Dashboard: `http://localhost:3000`; API: `http://localhost:8080`; OpenAPI chỉ mở trong Development tại `http://localhost:8080/openapi/v1.json`. Tài khoản thử: tổ chức `demo`, `admin@sentinellan.local`, mật khẩu đúng bằng `SENTINELLAN_DEMO_ADMIN_PASSWORD` trong `.env`. Đổi mật khẩu mẫu trước khi cho máy khác truy cập môi trường này.

Nếu readiness trả 503, kiểm tra `docker compose logs postgres api`. `health/live` chỉ xác nhận tiến trình còn chạy; `health/ready` thực sự thử kết nối database. Sau khi sửa `.env`, khởi động lại dịch vụ bằng `docker compose up -d --build`.

## Phát triển không dùng Docker cho API/web

Cần .NET SDK theo [global.json](global.json), Node/npm theo [package.json](package.json), và một PostgreSQL cục bộ nếu muốn kiểm tra migration. Không cấu hình `ConnectionStrings__SentinelLAN` thì API Development dùng EF InMemory; cách này chỉ phù hợp unit/E2E, không xác nhận ràng buộc PostgreSQL.

```powershell
./scripts/bootstrap.ps1
dotnet run --project apps/backend/src/SentinelLAN.Api --no-launch-profile
npm.cmd run dev
```

API và web chạy ở hai terminal. Trên Linux/macOS dùng `./scripts/bootstrap.sh`, `./scripts/dev.sh` hoặc lệnh tương đương. Cấu hình bí mật dùng biến môi trường hoặc user-secrets, không commit `.env`.

## Triển khai thật trên VPS

Production cần DNS trỏ tới VPS, chứng chỉ TLS tin cậy cho cùng DNS, Docker Compose, PostgreSQL và các khóa ngẫu nhiên. Không có tài khoản `demo`, enrollment token hoặc khóa mặc định ở Production. Trên cơ sở dữ liệu rỗng, API tạo **một** tổ chức và Admin từ các biến `BOOTSTRAP_*`; sau đó Admin cấp tài khoản Employee/Technician và token đăng ký một lần trong dashboard.

Đặt `fullchain.pem` và `privkey.pem` vào `deploy/vps/certs/` (thư mục bị Git/Docker bỏ qua), rồi chạy từ thư mục repo:

```bash
export PUBLIC_DOMAIN=sentinel.example.com
export BOOTSTRAP_ORG_CODE=example
export BOOTSTRAP_ORG_NAME='Example Organization'
export BOOTSTRAP_ADMIN_EMAIL=admin@example.com
sudo --preserve-env=PUBLIC_DOMAIN,BOOTSTRAP_ORG_CODE,BOOTSTRAP_ORG_NAME,BOOTSTRAP_ADMIN_EMAIL bash deploy/vps/setup-vps.sh
```

Script tạo `deploy/vps/.env` với quyền `0600` và khóa ngẫu nhiên, kiểm tra Compose, khởi động dịch vụ rồi thử `https://$PUBLIC_DOMAIN/health/ready`. Nó không in mật khẩu ra terminal, không tự cài Docker, không tự cấp chứng chỉ và không dừng toàn bộ stack trước khi cập nhật. Lấy mật khẩu bootstrap từ `.env` bằng terminal quản trị được bảo vệ, đăng nhập với mã tổ chức đã đặt, rồi lưu mật khẩu trong kho bí mật của tổ chức. Giữ `SERVER_VAULT_KEY` để giải mã dữ liệu VPS sau khôi phục; mất khóa này thì backup DB không đủ để đọc SSH key đã mã hóa. [Runbook triển khai](docs/deployment/saas-vps-guide.md) cần được đối chiếu với môi trường mạng thực tế.

Không đưa API, PostgreSQL hoặc cổng Agent HTTP trực tiếp ra Internet. Reverse proxy Production phục vụ HTTPS 443 và chuyển HTTP 80 sang HTTPS. Cookie xác thực trong Production có cờ `Secure`; Agent ngoài loopback chỉ dùng HTTPS. Bật tường lửa và kiểm tra chứng chỉ trước khi đăng ký thiết bị.

## Luồng chức năng thật

1. **Admin** đăng nhập, tạo tài khoản (link kích hoạt chỉ hiện một lần), cấp token enrollment 1–60 phút, xem inventory/telemetry/audit, gán hoặc thu hồi máy. API kiểm tra quyền, tenant, lý do và xác nhận; token/secret chỉ lưu dạng hash ở server.
2. **Agent** dùng token đăng ký một lần, lưu credential riêng, gửi heartbeat/CPU/RAM/disk và nhận lệnh có chữ ký khi được cấu hình khóa. Telemetry lỗi mạng được giữ trong hàng đợi bộ nhớ giới hạn, thử lại với cùng idempotency key; hàng đợi **chưa bền qua restart**. Lệnh `SimulateLock`, `SimulateNetworkIsolation`, `RestartService`, `RefreshPolicy` và `ShowNotification` hiện chỉ trả kết quả mô phỏng, không thay đổi OS hoặc áp dụng policy.
3. **Technician** xem thiết bị, xử lý cảnh báo, incident, work order và loan trong tenant. Work order đang mở được xếp theo Critical → High → Medium → Low, hạn đến rồi thời điểm tạo. Tham chiếu người dùng, incident và thiết bị được kiểm tra cùng tenant.
4. **Employee** xem `/my-device`, telemetry được công bố, chính sách gán và sự cố của máy mình; báo sự cố qua web/mobile. Khi Admin thu hồi máy, quyền xem máy và credential Agent bị từ chối.

Dashboard và API thực hiện các thao tác quản trị/lập phiếu bằng dữ liệu thật. Các giá trị `demo` chỉ được seed trong Development. Không coi policy cấu hình là bằng chứng Agent đã khóa USB hay mạng.

Khi thêm VPS, Admin cần nhập SSH host-key fingerprint dạng `SHA256:<43 ký tự base64>`, lấy và xác minh qua console tin cậy của nhà cung cấp (ví dụ chạy `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256` trên chính VPS). API chỉ thử SSH, đọc số liệu hoặc restart service nếu host key khớp pin. Các node đã tạo trước khi có trường này bị chặn SSH; xóa và đăng ký lại sau khi xác minh fingerprint. Private key được mã hóa ở server, không phải kho “zero knowledge”.

Dùng tài khoản SSH riêng có quyền tối thiểu; chỉ cấp `sudo systemctl restart` cho các dịch vụ thực sự cần vận hành. Không dùng tài khoản `root` mặc định trên biểu mẫu. Thử kết nối và quyền restart trên VPS thử nghiệm trước khi cấp quyền cho Production.

Adapter SSH dùng `sudo -n` để không chờ mật khẩu tương tác, giới hạn thời gian lệnh và kiểm tra dịch vụ `is-active` sau khi restart. Quyền sudo phải được cấu hình tương ứng trong `sudoers` của VPS.

Restart VPS yêu cầu lý do, checkbox xác nhận, nonce riêng và hạn tối đa năm phút. PostgreSQL lưu nonce với ràng buộc duy nhất trước khi SSH chạy, nên gửi lại cùng yêu cầu không chạy lệnh lần hai.

Tem QR chứa mã ngẫu nhiên chỉ trả một lần khi phát hành. Hãy in ngay trong phiên đó; sau khi tải lại trang, dashboard chỉ hiển thị trạng thái và tiền tố, không thể dựng lại mã từ tiền tố. Nếu cần in lại, xoay vòng tem để vô hiệu mã cũ.

## Cài Agent

Admin cấp enrollment token trên dashboard, sau đó publish Agent tương ứng:

```powershell
dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
./deploy/agent/install-windows-agent.ps1 -ServerUrl 'https://sentinel.example.com'
```

Installer Windows hỏi token mà không cần truyền trên command line và xóa biến token toàn máy sau khi identity được ghi. Với Linux, publish `-r linux-x64 -o publish/linux-x64`, sau đó chạy `sudo bash deploy/agent/install-linux-agent.sh --server https://sentinel.example.com`. Linux installer tạo user dịch vụ riêng và khóa mã hóa identity cục bộ; lần cài lại giữ khóa cũ. Không chuyển khóa này sang máy khác. Hướng dẫn thêm: [Agent enrollment](docs/guides/agent-enrollment-guide.md).

## Sao lưu, khôi phục và ứng phó

Sao lưu PostgreSQL bằng custom dump; các script không đụng vào database đang chạy khi diễn tập restore:

```bash
bash scripts/backup-postgres.sh deploy/vps/docker-compose.prod.yaml deploy/vps/.env
bash scripts/restore-drill-postgres.sh backups/postgres/<file>.dump deploy/vps/docker-compose.prod.yaml deploy/vps/.env
```

Đặt `SENTINELLAN_BACKUP_DIR` để đưa dump ra volume lưu trữ riêng. Mã hóa bản sao, giữ khóa vault riêng, sao chép offsite và diễn tập khôi phục định kỳ. `pg_dump` cho snapshot nhất quán và `pg_restore` kiểm tra được archive theo [tài liệu PostgreSQL](https://www.postgresql.org/docs/current/app-pgdump.html). Script restore chỉ tạo rồi xóa database thử có tên ngẫu nhiên; không ghi đè database ứng dụng. Khi nghi ngờ xâm nhập: bảo toàn log/bằng chứng, cô lập cổng public tại proxy/firewall, thu hồi credential thiết bị/phiên, xoay khóa theo [runbook sự cố](docs/security/incident-workflow.md); không xóa audit để che dấu vết.

## Kiểm tra chất lượng

```powershell
dotnet restore SentinelLAN.slnx
dotnet build SentinelLAN.slnx
dotnet test SentinelLAN.slnx
npm.cmd ci
npm.cmd run lint
npm.cmd run typecheck
npm.cmd test
npm.cmd run build
npm.cmd run typecheck:mobile
npm.cmd run lint:mobile
npm.cmd run test:mobile
```

E2E web: `npm.cmd run test:e2e`. Nếu Windows Application Control chặn DLL Release, đặt `SENTINELLAN_E2E_API_COMMAND='dotnet run --project ../backend/src/SentinelLAN.Api --no-launch-profile'` trong môi trường chạy Playwright. Docker/PG thật, chứng chỉ TLS, DNS, email phân phối link kích hoạt và khôi phục backup cần được xác nhận ở hạ tầng của bạn; kết quả unit test không thay thế nghiệm thu vận hành.
