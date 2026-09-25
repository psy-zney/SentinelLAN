# SentinelLAN

SentinelLAN quản lý thiết bị đầu cuối được tổ chức cho phép: Admin cấp tài khoản và mã đăng ký, Agent gửi telemetry kỹ thuật, nhân viên xem máy được giao và báo sự cố, kỹ thuật viên xử lý cảnh báo/công việc. Dữ liệu của mỗi tổ chức được giới hạn bằng `OrganizationId`. Các lệnh khóa máy và cô lập mạng vẫn **chỉ mô phỏng**; không bật hành vi thay đổi hệ điều hành trong bản này.

**Phạm vi chính:** một luồng quản lý thiết bị được ủy quyền, từ đăng ký Agent → heartbeat/telemetry → gán thiết bị → xử lý sự cố và ghi audit. Web và mobile là hai giao diện của cùng luồng này. Quản trị VPS qua SSH là chức năng mở rộng đã có; không phải mục tiêu chính của đồ án. SentinelLAN không quét toàn bộ LAN, bắt gói tin hay tự động cô lập thiết bị. [Phạm vi và tiêu chí kiểm chứng](docs/project-overview.md) là điểm tham chiếu khi thêm tính năng hoặc viết báo cáo.

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

Ranh giới kiến trúc và luồng chi tiết nằm trong [bản đồ hệ thống HTML](docs/system-map.html), [tổng quan dự án](docs/project-overview.md) và [ADR](docs/adr/0002-modular-monolith.md). [SQL PostgreSQL](docs/database/schema.postgresql.sql) được sinh từ EF Core migrations. Tài liệu trong `docs/local/`, `docs/private/`, chứng chỉ, dump, `.env` và ghi chú hạ tầng cá nhân không được đưa vào Git hoặc Docker build context.

## Chạy cục bộ bằng Docker Compose

Cần Docker Engine/Desktop và Compose v2. Đây là cấu hình **Development**; nó tạo dữ liệu `demo` để thử luồng, không dùng để công bố Internet.

```bash
git clone https://github.com/psy-zney/SentinelLAN.git
cd SentinelLAN
cp .env.example .env
# Fill SENTINELLAN_SIGNING_KEY, SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY,
# and SENTINELLAN_SERVER_VAULT_KEY with three distinct random secrets.
docker compose up --build -d
docker compose ps
curl --fail http://localhost:8080/health/live
curl --fail http://localhost:8080/health/ready
```

Tạo ba khóa khác nhau bằng cách chạy `openssl rand -hex 32` ba lần rồi điền vào `.env`; trên PowerShell có thể chạy `[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` ba lần. Compose từ chối khởi động nếu thiếu một trong ba khóa. Trên PowerShell, thay `cp` bằng `Copy-Item .env.example .env` và dùng `Invoke-WebRequest http://localhost:8080/health/ready`. Dashboard: `http://localhost:3000`; API: `http://localhost:8080`; OpenAPI chỉ mở trong Development tại `http://localhost:8080/openapi/v1.json`. Tài khoản thử: tổ chức `demo`, `admin@sentinellan.local`, mật khẩu đúng bằng `SENTINELLAN_DEMO_ADMIN_PASSWORD` trong `.env`. Đổi mật khẩu mẫu trước khi cho máy khác truy cập môi trường này. Giữ khóa vault cục bộ nếu muốn đọc lại SSH key đã mã hóa sau khi tạo lại container.

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
2. **Agent** dùng token đăng ký một lần, lưu credential riêng, gửi heartbeat/CPU/RAM/disk và nhận lệnh có chữ ký khi được cấu hình khóa. Production giữ tối đa 50 heartbeat trong file được mã hóa, tối đa một giờ, và khôi phục queue sau restart; Development mặc định dùng RAM. Retry giữ nguyên idempotency key. Server cho phép giao lại cùng lệnh sau lease 30 giây nếu poll response bị mất; Agent giữ nonce và tối đa một biên lai đang chờ trong file bảo vệ, rồi gửi lại biên lai sau restart trước khi poll tiếp. Lệnh `SimulateLock`, `SimulateNetworkIsolation`, `RestartService`, `RefreshPolicy` và `ShowNotification` hiện chỉ trả kết quả mô phỏng, không thay đổi OS hoặc áp dụng policy.
3. **Technician** xem thiết bị, xử lý cảnh báo, incident, work order và loan trong tenant. Work order đang mở được xếp theo Critical → High → Medium → Low, hạn đến rồi thời điểm tạo. Tham chiếu người dùng, incident và thiết bị được kiểm tra cùng tenant.
4. **Employee** xem `/my-device`, telemetry được công bố, chính sách gán và sự cố của máy mình; báo sự cố qua web/mobile. Khi Admin thu hồi máy, quyền xem máy và credential Agent bị từ chối.

Dashboard và API thực hiện các thao tác quản trị/lập phiếu bằng dữ liệu thật. Các giá trị `demo` chỉ được seed trong Development. Không coi policy cấu hình là bằng chứng Agent đã khóa USB hay mạng.

### Mở rộng hiện có: quản trị VPS qua SSH

Luồng này phục vụ máy chủ Linux được đăng ký riêng; không phải điều kiện để chạy luồng Agent và quản lý endpoint ở trên. Khi thêm VPS, Admin cần nhập SSH host-key fingerprint dạng `SHA256:<43 ký tự base64>`, lấy và xác minh qua console tin cậy của nhà cung cấp (ví dụ chạy `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256` trên chính VPS). API chỉ thử SSH, đọc số liệu hoặc restart service nếu host key khớp pin. Các node đã tạo trước khi có trường này bị chặn SSH; xóa và đăng ký lại sau khi xác minh fingerprint. Private key được mã hóa ở server, không phải kho “zero knowledge”.

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

Installer Windows hỏi token mà không cần truyền trên command line và xóa biến token toàn máy sau khi identity được ghi. Với Linux, publish `-r linux-x64 -o publish/linux-x64`, sau đó chạy `sudo bash deploy/agent/install-linux-agent.sh --server https://sentinel.example.com`. Installer tạo user dịch vụ riêng và tự sinh `SENTINELLAN_AGENT_STORE_KEY` ngẫu nhiên, lưu trong `/etc/sentinellan/agent.env` với quyền đọc/ghi hạn chế; cài lại giữ khóa cũ. Khóa này bảo vệ identity, queue và nonce store bằng AES-GCM, không chuyển sang máy khác. Khi tự triển khai không dùng installer, phải cấp một khóa riêng tối thiểu 32 ký tự cho service environment. Hướng dẫn thêm: [Agent enrollment](docs/guides/agent-enrollment-guide.md).

## Sao lưu, khôi phục và ứng phó

Sao lưu PostgreSQL bằng custom dump; các script không đụng vào database đang chạy khi diễn tập restore:

```bash
bash scripts/backup-postgres.sh deploy/vps/docker-compose.prod.yaml deploy/vps/.env
bash scripts/restore-drill-postgres.sh backups/postgres/<file>.dump deploy/vps/docker-compose.prod.yaml deploy/vps/.env
```

Đặt `SENTINELLAN_BACKUP_DIR` để đưa dump ra volume lưu trữ riêng. Mã hóa bản sao, giữ khóa vault riêng, sao chép offsite và diễn tập khôi phục định kỳ. `pg_dump` cho snapshot nhất quán và `pg_restore` kiểm tra được archive theo [tài liệu PostgreSQL](https://www.postgresql.org/docs/current/app-pgdump.html). Script restore chỉ tạo rồi xóa database thử có tên ngẫu nhiên; không ghi đè database ứng dụng. Khi nghi ngờ xâm nhập: bảo toàn log/bằng chứng, cô lập cổng public tại proxy/firewall, thu hồi credential thiết bị/phiên, xoay khóa theo [runbook sự cố](docs/security/incident-workflow.md); không xóa audit để che dấu vết.

## Kiểm tra chất lượng

### Ứng dụng mobile

Ứng dụng Employee nằm trong `apps/mobile`. Build Development dùng `EXPO_PUBLIC_API_URL` trỏ đến API mà emulator/điện thoại truy cập được; `localhost` trên điện thoại là chính điện thoại. Bản EAS preview/production yêu cầu `EXPO_PUBLIC_API_URL` và `EXPO_PUBLIC_WEB_URL` là HTTPS origin thật (nếu web và API cùng origin, có thể đặt cùng giá trị). Domain web phải phục vụ file liên kết ứng dụng của Android/iOS cho đường dẫn `/activate` trước khi link HTTPS có thể mở app; app cũng hỗ trợ scheme `sentinellan://activate`. Không đưa token kích hoạt vào log hoặc fixture công khai. Xem [hướng dẫn kiểm thử thiết bị](apps/mobile/e2e/README.md) để chạy các flow đăng nhập, quét QR, báo sự cố, ngoại tuyến và đăng xuất trên Android.

Các lệnh lint, typecheck, Jest và Expo export chỉ xác nhận mã nguồn cùng JavaScript bundle. Cần build native và chạy trên thiết bị đích để nghiệm thu camera, chọn ảnh, SecureStore, deep link và kết nối qua mạng thật.

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
