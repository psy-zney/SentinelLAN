# Kết quả kiểm tra ngày 2026-09-26

Kiểm tra working tree chứa thay đổi Agent Windows thật, trên nền commit `e11b31c`; các thay đổi chưa commit. Tổng cộng **249 test tự động pass**. Đây là kết quả kiểm tra mã và kết nối cục bộ, chưa phải chứng nhận triển khai production hoặc nghiệm thu mọi hành động trên thiết bị thật.

## Môi trường và kết quả

Windows, .NET SDK 10.0.400, Node.js 24.18.0, Docker Engine 29.8.0, Compose v5.5.1 và PostgreSQL 18.1 Alpine. Stack có sẵn của người dùng vẫn chạy; các bài thử SQL và Docker tạo container/database/project riêng và xóa tài nguyên thử nghiệm sau khi kết thúc.

| Kiểm tra | Kết quả | Phạm vi thực tế |
|---|---|---|
| Restore/build .NET Release | PASS | Không warning/error |
| Architecture | 1/1 | Hướng phụ thuộc giữa các layer |
| Domain | 5/5 | Quy tắc và trạng thái nghiệp vụ |
| Application | 68/68 | Use case, quyền, tenant, policy, QR và quản trị |
| Agent | 41/41 | Chữ ký, expiry/replay, receipt/retry, thu telemetry, lab guard; một số OS adapter được thay thế trong unit test |
| Integration với PostgreSQL thật | 55/55 | Migration, xác thực, enrollment dùng một lần, tenant, idempotency và concurrency trên provider SQL thật |
| Web lint/typecheck/unit/build | PASS; 25/25 test | Next.js production build |
| Mobile lint/typecheck/Jest | PASS; 48/48 test | 7 suite; chưa chạy native trên điện thoại |
| Expo export Android/iOS | PASS | JavaScript bundle; không phải APK/IPA hay nghiệm thu camera/SecureStore |
| Playwright Chromium | 6/6 | Đăng nhập, quản trị, enrollment, telemetry/SignalR, command mô phỏng, audit, heartbeat timeout, lỗi API và QR |
| Docker Compose build/run | PASS | Image API/web build từ mã hiện tại; PostgreSQL healthy; HTTP live/ready/login trả 200 |
| Windows Agent → API → PostgreSQL | PASS | Hai tiến trình .NET riêng, HTTP thật, enrollment thật, signed `CollectTelemetryNow`, mẫu OS thật và receipt thành công |
| PostgreSQL backup/restore | PASS | `pg_dump --format=custom`, restore vào database mới, đối chiếu số bản ghi |
| NuGet vulnerable, gồm transitive | PASS | Không advisory được báo bởi nguồn đang dùng |
| npm production dependency audit | Exit 0; còn cảnh báo | 13 moderate, không high/critical; chưa xử lý hết |
| Format toàn solution | FAIL | Encoding/newline/namespace ở migration lịch sử |
| Format ngoài thư mục migration | PASS | Không bỏ qua các file mã ứng dụng hoặc test |
| Production readiness | BLOCKED | Thiếu cấu hình và thiết bị nghiệm thu như mô tả bên dưới |

## Bằng chứng kết nối và lưu trữ thật

Test .NET dùng container PostgreSQL riêng với database rỗng; factory integration sử dụng provider Npgsql thay cho InMemory. Các bài thử concurrency/idempotency chạy trên SQL thật. Kết quả TRX cục bộ nằm trong `scratch/test-results/`, không đưa vào Git.

Stack Compose thử nghiệm dùng HTTP API cổng 18082 và web cổng 13300, tách khỏi stack của người dùng ở 8080/3000. API container thực hiện migration và kiểm tra kết nối database. Bài thử native Agent chạy API host riêng ở 18083, cùng PostgreSQL trong project Docker thử nghiệm; đây là hai kết nối khác nhau, không khẳng định Agent đã chạy bên trong container hay kết nối trực tiếp API container.

Kiểm tra cuối xác nhận stack có sẵn ở 8080/3000 vẫn trả HTTP 200 và PostgreSQL healthy. OpenAPI của API đang chạy chưa có `/api/v1/agent/policy`, cho thấy stack có sẵn chưa chứa đầy đủ thay đổi hiện tại và cần rebuild/cập nhật để dùng các thay đổi này. Kết quả build/test của stack riêng không đồng nghĩa image đang phục vụ người dùng đã được cập nhật.

Lần thử cuối quan sát **3 mẫu telemetry của thiết bị thử** qua API. Một mẫu: CPU **34.29%**, RAM **90.15%**, disk **74.28%**. Giá trị là một lần lấy mẫu trên host lúc chạy bài thử, không phải benchmark hoặc đối chiếu độ chính xác với Task Manager. Receipt `CollectTelemetryNow` xác nhận HTTP chấp nhận mẫu mới.

Backup và restore hoàn tất vào database riêng. Số bản ghi nguồn và đích đều là `14|5|1|4` theo thứ tự `__EFMigrationsHistory|Telemetry|CommandResults|AuditLogs`. `Telemetry` gồm cả dữ liệu seed; số bản ghi tổng không đồng nghĩa tất cả là mẫu của thiết bị thử. Đây là đối chiếu số lượng bản ghi và archive restore, chưa phải diễn tập toàn bộ triển khai với vault key, offsite storage hoặc mục tiêu RPO/RTO.

## Lỗi phát hiện và đã sửa

- E2E dựa vào lệnh mặc định cũ `SimulateLock`, trong khi UI hiện mặc định `CollectTelemetryNow`. Test giờ chọn `SimulateLock` rõ ràng để đúng mục đích kiểm tra luồng mô phỏng; cả 6 E2E chạy lại pass.
- Script live Agent bổ sung `-UsePostgres` với guard tên database thử nghiệm. Sửa cách gọi setter/indexer `DbConnectionStringBuilder` để Windows PowerShell đọc đúng chuỗi kết nối, thay vì diễn giải property như khóa dictionary. Bài thử PostgreSQL sau sửa pass.
- Sửa format một số file test. Không thay đổi migration đã có chỉ để làm xanh formatter; lỗi format ở migration vẫn được báo là FAIL.
- Harness Docker cục bộ sửa truyền tham số `pg_restore` và chuyển SQL qua stdin để giữ dấu nháy identifier trên Windows PowerShell. Backup/restore sau sửa pass.

## Các điều kiện chưa đạt

`test-production-readiness.ps1 -ServerUrl http://127.0.0.1:8080 -CheckWindowsAgent` trả exit 1: Docker đã READY, nhưng HTTP không đáp ứng yêu cầu HTTPS production. Không tìm thấy Windows Service `SentinelLANAgent`, desktop companion hoặc enrollment ở vị trí production mặc định; chưa có cờ lab/device ID được cấu hình. Machine environment chưa có khóa xác minh command; nếu dùng file settings được bảo vệ, Admin cần kiểm tra nguồn cấu hình đó tại host.

Chưa nghiệm thu thao tác `ShowNotification`, `LockWorkstation`, `IsolateNetwork`, restart service và áp policy trên thiết bị đích thật. Unit test lab guard và script firewall/recovery có thay thế OS boundary; chúng không chứng minh màn hình đã khóa, USB bị chặn, traffic bị cô lập hay Task Scheduler tự khôi phục sau reboot. Chưa chạy MSI/service installation hoặc mobile native với camera, SecureStore và deep link trên điện thoại. Hướng dẫn nghiệm thu nằm trong [thực thi Agent thật](real-agent-execution.md).

Audit npm vẫn báo hai advisory qua 13 package trong cây Expo/mobile: `GHSA-vcc3-ghjq-m6fr` và `GHSA-w5hq-g745-h8pq`; xem [risk register](../security/dependency-risk-register.md). Lockfile SHA-256 lúc kiểm tra: `DC3F9491DC228714242FB8AC0CCB2D0D374AE99A65C2A7492B171A3ACB758391`. Không nâng cấp phá vỡ tương thích tự động trong đợt kiểm tra này.

Khóa HMAC dùng chung, rotation/public-key pinning và bảo vệ audit bất biến ở database vẫn cần đánh giá trước production theo [ADR 0007](../adr/0007-real-windows-agent-actions.md). Chưa kiểm tra VPS SSH, TLS/DNS thực tế, phân phối email, offsite backup hoặc vận hành dài hạn.

## Chạy lại

`scripts/test-all.ps1 -RequirePostgres` chạy release gate khi `ConnectionStrings__SentinelLAN` trỏ tới database thử nghiệm riêng; đặt `SENTINELLAN_E2E_API_URL` và `SENTINELLAN_E2E_WEB_URL` sang cổng trống nếu 8080/3100 đã dùng. Phiên này chạy từng nhóm để hoàn thành tất cả nhóm dù một bước ban đầu thất bại, rồi chạy lại nhóm sau sửa.

Sau build Debug, `scripts/test-agent-live.ps1 -UsePostgres -Port 18083` kiểm tra native Agent/HTTP/SQL. Phải cấp chuỗi kết nối tới database mới tên `sentinellan_agent_live_<hex ngẫu nhiên>` qua environment. Mặc định khi không có `-UsePostgres`, script báo rõ persistence InMemory. Bài thử không bật lock, firewall, restart service hoặc policy.

Kiểm tra format có giới hạn đã chạy: `dotnet format SentinelLAN.slnx --no-restore --verify-no-changes --exclude apps/backend/src/SentinelLAN.Infrastructure/Migrations`. Lệnh bỏ `--exclude` vẫn thất bại ở migration lịch sử; không coi kiểm tra có giới hạn là full format pass.
