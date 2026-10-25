# Rà soát SentinelLAN cho đồ án tốt nghiệp — 23/10/2026

> Các lỗi được xác nhận đã được sửa trong vòng tiếp theo. Xem [báo cáo verification mới nhất, 24/10/2026](remediation-verification-2026-10-24.md) để biết kết quả kiểm thử, coverage và giới hạn còn lại. Các số đo bên dưới thuộc lần review trước khi sửa.

**Kết luận:** phù hợp với đề tài **“Hệ thống quản lý và giám sát bảo mật thiết bị đầu cuối trong mạng LAN”**. Chưa đủ bằng chứng để đánh dấu hoàn tất nghiệm thu: còn hai lỗi vận hành, coverage chưa đạt mục tiêu 80%, một số kiểm thử Windows bị giới hạn môi trường và PostgreSQL thật chưa được kiểm chứng trong lần chạy này.

Đánh giá trên working tree tại HEAD `0c309a4`, gồm 76 tệp tracked đã sửa và 26 tệp mới trước khi thêm báo cáo. Dùng `verification-loop`, `sentinellan-dev` và ba agent phụ trách backend/bảo mật, web/mobile, kiến trúc/đồ án. Rà soát nguồn có mục tiêu, ưu tiên các thay đổi hiện tại và ranh giới xác thực, tenant, lệnh, quota, giao dịch; không phải kiểm toán bảo mật toàn diện. Không sửa source, lockfile, dependency hay cấu hình triển khai.

## Báo cáo sáu bước

| Bước | Kết quả | Bằng chứng |
|---|---|---|
| Build | PASS | Solution .NET Release: 0 warning, 0 error; production build company và employee đạt. Chưa build native mobile. |
| Types | PASS | Typecheck web và mobile đạt; C# biên dịch thành công. |
| Lint | PASS | Lint web/mobile đạt, không warning. Không chạy `dotnet format`. |
| Tests và coverage | FAIL / còn giới hạn | .NET sau khi chạy lại Agent: 352 đạt, 9 lỗi, 2 bỏ qua / 363. Web 44/44, mobile 76/76, Python 20/20, regression backport 12/12. Coverage chi tiết bên dưới. |
| Security | FAIL quality gate tổng | Gate dependency production có xác minh backport đạt; còn hai advisory Moderate ở production dependency tree và cảnh báo tooling chưa xử lý khi quét cả dev. Có một lỗi limiter được xác nhận từ nguồn; chưa xác nhận lỗ hổng khai thác ứng dụng từ các advisory. |
| Diff | PASS kiểm tra whitespace; review có mục tiêu | `git diff --check` đạt. Không khẳng định đã đọc từng dòng của toàn bộ 102 tệp thay đổi. |

**Overall: NOT READY để đánh dấu toàn bộ quality gate đã đạt.** Điều này không phủ nhận tính phù hợp của đề tài; các tiêu chí nghiệm thu còn thiếu được liệt kê riêng.

## Hai lỗi cần sửa

### 1. P2 — Limiter cấp token thấp hơn tốc độ cấu hình

Nguồn: [IngressRateLimitMiddleware.cs:33](../../apps/backend/src/SentinelLAN.Api/IngressRateLimitMiddleware.cs#L33), đặc biệt dòng 45–47.

Mặc định `SENTINELLAN_INGRESS_REQUESTS_PER_MINUTE=600`, `SENTINELLAN_INGRESS_BURST=300`, nhưng limiter dùng `TokenLimit=300`, `TokensPerPeriod=600`, bổ sung một lần mỗi phút. Số token trong bucket luôn bị chặn ở 300. Khi có lưu lượng liên tục, tốc độ cấp dài hạn chỉ khoảng 300/phút/IP, cùng burst ban đầu; tiêu thụ hết bucket sẽ phải chờ lần bổ sung tiếp theo. Đây là suy luận trực tiếp từ cấu hình và định nghĩa [TokenLimit của Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting.tokenbucketratelimiteroptions.tokenlimit).

Hệ quả có điều kiện: Agent/dashboard cùng IP qua NAT có thể nhận 429 dù lưu lượng dài hạn chưa vượt mức 600/phút được cấu hình. Chưa thực hiện kiểm thử tải hay chứng minh vi phạm phân quyền từ lỗi này.

Sửa đề xuất: bổ sung token theo chu kỳ nhỏ, tính từ tốc độ cấu hình; giữ burst là sức chứa độc lập và xử lý chính xác tốc độ có phần lẻ. Thêm kiểm thử phân biệt tốc độ dài hạn với burst, cấu hình `burst < perMinute` và nhiều client dùng chung IP.

### 2. P2 — Script test tổng chặn lại các advisory đã được backport

Nguồn: [test-all.ps1:25](../../scripts/test-all.ps1#L25), [test-all.sh:20](../../scripts/test-all.sh#L20), [audit.mjs:49](../../scripts/dependency-security/audit.mjs#L49).

Hai script chạy `audit:dependencies`, sau đó lại chạy `npm audit --omit=dev --audit-level=high`. Kiểm tra trực tiếp trên working tree này:

- `npm.cmd run audit:dependencies`: exit 0; 12/12 regression test đạt; xác minh từng bản cài braces/node-forge có mã vá.
- `npm.cmd audit --omit=dev --audit-level=high --json`: exit 1; hai advisory gốc High là braces và node-forge mà gate trước vừa xác minh bản vá.

Vì raw audit chỉ nhận diện version/advisory, script tổng vẫn thất bại tại gate dư này ngay cả khi các bước trước đã đạt. Không chạy toàn bộ script thêm lần nữa; kết luận dựa trên lệnh gate được chạy trực tiếp và cơ chế dừng khi exit khác 0 của hai script.

Sửa đề xuất: giữ `audit:dependencies` làm gate; bỏ lệnh raw audit dư hoặc lưu nó như báo cáo không quyết định pass/fail. Gate hiện có vẫn chặn mọi High/Critical chưa được xác minh đã vá, không được thay bằng bỏ qua lỗi audit toàn cục. `.github/workflows/ci.yml:105` chỉ dùng gate có xác minh nên không mắc lỗi lặp này; không suy rộng lỗi sang workflow release Agent.

## Kiểm thử và độ bao phủ

| Bộ test | Đạt | Lỗi | Bỏ qua | Ghi chú |
|---|---:|---:|---:|---|
| Application | 65 | 0 | 0 | .NET Release |
| Domain | 23 | 0 | 0 | .NET Release |
| Architecture | 1 | 0 | 0 | Hiện chỉ kiểm tra Domain không có ProjectReference |
| Integration | 178 | 0 | 2 | EF InMemory; chưa có `ConnectionStrings__SentinelLAN` |
| Agent, lần chạy lại | 85 | 9 | 0 | TEMP/TMP nằm trong workspace |
| Web | 44 | 0 | 0 | 9 suite |
| Mobile | 76 | 0 | 0 | 12 suite, chạy tuần tự |
| Python deploy helpers | 20 | 0 | 0 | Runtime Python bundled; TEMP/TMP trong workspace |
| Dependency backports | 12 | 0 | 0 | Chạy trong `audit:dependencies` |

Lần .NET đầu có 25 Agent test lỗi. Chuyển TEMP/TMP vào thư mục scratch giải quyết 16 ca lỗi quyền thao tác tệp tạm. Chín ca còn lại thuộc đọc trạng thái phiên Windows và các scenario PowerShell setup/firewall harness: `Access is denied`, `AuthorizationManager check failed`, hoặc thiếu output do script không chạy. Không quy chúng thành lỗi chức năng đã xác nhận; cần chạy lại trên máy Windows/lab có quyền và chính sách thực thi phù hợp. Không tắt chính sách máy trong lần duyệt này.

Hai test PostgreSQL bị bỏ qua là rollback asset khi ghi audit thất bại và ngân sách login dùng chung giữa database scope. Không dùng kết quả InMemory để kết luận đúng transaction/advisory lock/concurrency PostgreSQL. Docker CLI có nhưng daemon không chạy (`dockerDesktopLinuxEngine` không tồn tại), nên không dựng được PostgreSQL cô lập bằng Docker trong môi trường hiện tại.

Mobile có một lần ScannerView timeout 5 giây khi chạy coverage đầu tiên; chạy lại riêng với timeout gốc đạt, rồi chạy lại toàn bộ 76 test đạt. Cần theo dõi độ ổn định CI, chưa đủ bằng chứng xem là lỗi chức năng.

| Coverage | Kết quả và giới hạn |
|---|---|
| .NET line, hợp nhất các tệp đã instrument | 6.252/8.262 = **75,67%**, sau loại đường dẫn `/migrations/`, `/obj/`, `/bin/`; dưới mục tiêu 80%. Chuẩn hóa đường dẫn theo `<sources>` trước khi hợp nhất để không đếm lặp giữa suite. Có các ca sandbox lỗi; không đại diện các assembly/tệp chưa được instrument như Worker/Setup. |
| .NET raw gồm migration/generated | 6.338/40.236 = 15,75%; chỉ ghi để minh bạch mẫu số. |
| Mobile | Statements 64,03%; branches 55,79%; functions 48,17%; lines **69,21%**. `jest.config.js` chưa cấu hình `collectCoverageFrom` hay `coverageThreshold`, nên chỉ phản ánh các tệp được import khi test. |
| Web | Chưa đo được: `--coverage` báo thiếu `@vitest/coverage-v8`. Không cài thêm dependency trong lần review. |

Coverage .NET theo assembly, cùng phạm vi lọc ở trên: Agent.Core 90,82%; Agent.Infrastructure 46,40%; Api 79,50%; Application 81,17%; Domain 89,22%; Enrollment 91,89%; Infrastructure 79,75%. Không cộng số test giữa lần đầu và lần chạy lại.

## Kết quả quét dependency và bí mật

- NuGet: `dotnet list SentinelLAN.slnx package --vulnerable --include-transitive --no-restore --format json`, exit 0, không liệt kê advisory trong 14 project. Kết quả theo feed tại thời điểm kiểm tra.
- Production npm: gate có xác minh mã vá đạt. Còn [decode-uri-component](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr) và [uuid](https://github.com/advisories/GHSA-w5hq-g745-h8pq) mức Moderate. Chưa xác minh đường khai thác trong ứng dụng.
- Raw npm production: 51 package entry bị gắn cờ, 45 High và 6 Moderate; nhiều entry lan truyền cùng advisory. Hai advisory gốc High braces/node-forge đã được gate repository xác minh mã vá. Không diễn giải đây là 45 lỗ hổng High độc lập.
- Raw npm gồm dev: exit 1, 62 package entry (54 High, 8 Moderate). Ngoài các backport, có advisory Vite và `@vitest/mocker` cần xử lý trong tooling; ví dụ [advisory Windows của launch-editor/Vite](https://github.com/advisories/GHSA-698v-4r3x-3vcv). Chưa có đánh giá reachability toàn bộ, không coi mọi cảnh báo tooling là lỗ hổng của bản production.
- Quét mẫu private key/AWS/GitHub/OpenAI token trong các thư mục source/deploy/scripts được chọn không tìm thấy bí mật thực sau triage. Hai dòng trùng mẫu là placeholder `BEGIN OPENSSH PRIVATE KEY` với dấu `...` trong i18n, không phải khóa. Không đọc `.env`, credential hoặc log. Đây là quét mẫu có giới hạn, không phải chứng minh repository không có bí mật.
- Không thấy `console.log(` trong source web-ui/mobile được quét.

## Đánh giá theo mục tiêu đồ án

Điểm đã có cơ sở từ nguồn và tài liệu: backend chia Domain/Application/Infrastructure/API; Agent Core tách adapter OS; phân quyền người dùng và scheme Agent riêng; tenant/ownership; enrollment một lần; chữ ký lệnh RSA-PSS, nonce/hạn, lease/receipt retry; telemetry, inventory, policy, alert, hỗ trợ và audit. Đây là phạm vi hợp lý để trình bày bài toán quản trị và tư thế bảo mật endpoint. Chưa có cơ sở thực nghiệm để tuyên bố khả năng EDR/antivirus hay phát hiện xâm nhập toàn diện.

`SimulateLock`/`SimulateNetworkIsolation` là mô phỏng đúng thiết kế. Khóa/cô lập thật cần opt-in lab, đúng device và điều kiện quyền OS/recovery. Phải ghi rõ trạng thái mô phỏng khi demo. Audit append-only ở tầng ứng dụng không tương đương WORM; giới hạn vận hành đã công khai cần nằm trong phần hạn chế của luận văn.

Ưu tiên hoàn thiện trước bảo vệ:

1. Sửa hai lỗi P2 và bổ sung kiểm thử hồi quy có ý nghĩa; giữ nguyên kiểm tra High/Critical chưa vá.
2. Chạy PostgreSQL thật trong môi trường thử riêng: migration, hai test đang skip, transaction/concurrency; thực hiện backup/restore với dữ liệu giả.
3. Chạy lại chín ca Windows trên lab phù hợp, ghi rõ máy/OS/quyền; thực hiện browser E2E và smoke trên thiết bị mobile thật. Không dùng kết quả unit test thay cho bằng chứng triển khai.
4. Hoàn thiện coverage web; đặt phạm vi source/threshold rõ ràng cho mobile và .NET; ưu tiên failure path, tenant/authorization, retry/replay, adapter thay vì tăng số test phản chiếu implementation.
5. Mở rộng ArchitectureTests để kiểm tra Application chỉ phụ thuộc Domain, Agent Core độc lập infrastructure/OS và các ranh giới module. Test hiện tại chưa đủ chứng minh các điều này tự động.
6. Gắn nhãn lịch sử cho đoạn [real-agent-execution.md:55](../guides/real-agent-execution.md#L55) còn nói HMAC dùng chung ở mốc 26/9; liên kết thiết kế RSA hiện hành trong [command-delivery.md](../architecture/command-delivery.md).

Cải thiện UX nên cân nhắc: bulk action giữ lựa chọn qua trang/bộ lọc, có hiển thị tổng số nhưng chưa preview danh sách tên thiết bị trước gửi. Thêm bản xem trước giúp người vận hành nhận biết đích; chưa coi hành vi chọn xuyên trang là bug đã xác minh.

Demo bảo vệ 12–15 phút: hai tenant và Admin/Employee; enrollment thiết bị; telemetry/inventory; lệnh CollectTelemetryNow và audit/receipt; SimulateLock có nhãn; policy tạo alert; yêu cầu hỗ trợ; mất mạng ngắn rồi khôi phục receipt/poll. Các thao tác bảo vệ thật là bài lab riêng có recovery.

Thực nghiệm cần ghi commit/working tree, OS, số thiết bị, cấu hình server/DB và số lần lặp; đo p50/p95 heartbeat→dashboard và lệnh→receipt, tỷ lệ thành công, CPU/RAM Agent, thời gian phục hồi mất mạng và backup/restore. Bảng an toàn cần kết quả tenant/permission, token reuse, replay và expiry. Chưa đo thì ghi chưa đo.

## Lệnh và bằng chứng để chạy lại

```powershell
dotnet build SentinelLAN.slnx --configuration Release --no-restore --nologo --verbosity minimal -m:1 -nodeReuse:false -p:UseSharedCompilation=false
dotnet test SentinelLAN.slnx --configuration Release --no-build --no-restore --nologo --verbosity minimal --collect:'XPlat Code Coverage' --results-directory scratch/verification-2026-10-23/dotnet --logger:'trx;LogFilePrefix=verification' -m:1

# Chỉ đặt TEMP/TMP cho process kiểm chứng khi cần tránh lỗi thư mục tạm sandbox.
$verifyTemp = Join-Path (Get-Location) 'scratch\verification-2026-10-23\rerun-temp'
New-Item -ItemType Directory -Path $verifyTemp -Force | Out-Null
$env:TEMP = $verifyTemp
$env:TMP = $verifyTemp
dotnet test apps/agent/tests/SentinelLAN.Agent.Tests/SentinelLAN.Agent.Tests.csproj --configuration Release --no-build --no-restore --collect:'XPlat Code Coverage'

npm.cmd run build
npm.cmd run typecheck
npm.cmd run typecheck:mobile
npm.cmd run lint
npm.cmd run lint:mobile
npm.cmd run test --workspace=@sentinellan/web-ui
npm.cmd run test --workspace=@sentinellan/web-ui -- --coverage
npm.cmd run test:coverage --workspace=@sentinellan/mobile -- --runInBand --cacheDirectory ../../scratch/verification-2026-10-23/jest
npm.cmd run audit:dependencies
npm.cmd audit --omit=dev --audit-level=high --json
npm.cmd audit --json
dotnet list SentinelLAN.slnx package --vulnerable --include-transitive --no-restore --format json
git diff --check
```

Python được chạy bằng runtime bundled vì alias `python.exe` hệ thống không truy cập được: `python -B -m unittest discover -s deploy/vps/tests -v`, với TEMP/TMP trong workspace.

Bằng chứng local không đưa vào Git: `scratch/verification-2026-10-23/dotnet/` và `agent-retry/` chứa TRX/Cobertura; `summary.json` và `summarize.py` chứa thống kê/phương pháp gộp coverage. Mobile có `apps/mobile/coverage/coverage-final.json`, `lcov.info`, `lcov-report/index.html`. Cache tạm web/mobile đã được agent dọn. Lần build .NET song song đầu bị hủy; lệnh tuần tự ở trên đã build thành công. Chưa chạy restore/cài gói, triển khai, E2E trình duyệt hay native mobile build trong lần duyệt này.
