# Sửa lỗi và verification SentinelLAN — 24/10/2026

## Kết quả và phạm vi

Đã xử lý các lỗi được xác nhận trong hai báo cáo ngày 23/10, bổ sung kiểm thử hồi quy và sửa các lỗi phát hiện trong vòng kiểm chứng. Thực hiện theo `verification-loop`, `security-review`, `sentinellan-dev`, cùng agent phụ trách kiểm thử thiết bị, quản trị và realtime. Các thay đổi có sẵn trong working tree được giữ nguyên; chưa commit hoặc triển khai.

**Kết luận:** các kiểm tra mã nguồn và build cục bộ đạt. Nghiệm thu PostgreSQL và adapter OS thật còn giới hạn bên dưới; đây không phải chứng nhận bảo mật hoặc kết quả CI chạy trên GitHub.

## Các lỗi đã sửa

| Lỗi | Thay đổi và bằng chứng |
|---|---|
| Token bucket thực tế chỉ cấp 300 token/phút khi cấu hình 600 | Bucket nạp liên tục theo thời gian đơn điệu, giữ burst độc lập; clock giả kiểm tra trước/đúng thời điểm nạp, tốc độ 30/61/600/60000 và giới hạn burst. |
| DELETE thiếu body được tự gán lý do và xác nhận | Hai endpoint thu hồi activation token/QR yêu cầu JSON body không null. Body thiếu/null, lý do trắng hoặc `confirmed=false` trả 400, không thu hồi và không ghi audit. JSON lỗi trả 400 cả Development; OpenAPI xác nhận body bắt buộc. |
| Gate audit tổng chặn lại dependency đã backport | Script PowerShell/Bash và CI dùng gate có xác minh từng source backport, bao gồm runtime/dev và chặn Moderate+ chưa vá. Bỏ gate raw audit dư; không bỏ qua toàn bộ audit. |
| Dependency tooling và decoder chưa được vá tương thích | Vitest/coverage 4.1.12, Vite 8.3.5, bản vá Expo SDK 57; decoder dùng backport scanner tuyến tính có checksum, giữ CommonJS và tương thích legacy. 15 regression tests đạt. Không dùng `--force` hoặc `--legacy-peer-deps`. |
| Web thiếu CSP phù hợp Next.js SSR | Proxy tạo nonce riêng từng response, ghi đè header nonce/CSP từ client; layout render động; mọi script SSR có nonce. Nginx giữ CSP từ upstream. Chromium production xác nhận hydration, đổi ngôn ngữ, hiện mật khẩu và chặn inline event handler. |
| Mobile giữ access token trong RAM khi lưu refresh token thất bại | Chỉ công bố access token sau khi lưu SecureStore thành công; regression kiểm tra đường lỗi lưu session. |
| Chọn hàng ở trang thiết bị cũ còn tác động trang mới | Đặt lại selection khi thay trang/tìm kiếm/bộ lọc; hiện tên thiết bị trước xác nhận bulk; regression không gửi mục tiêu ẩn từ trang trước. |
| Employee mở hub chỉ cho Admin và lặp 403 | `useLiveQuery` hỗ trợ tắt realtime; hai panel của My Device gọi API cá nhân mà không mở hub Admin. Quyền backend giữ nguyên; kiểm tra cả Employee và Admin. |
| Phiếu bảo trì cho chọn giá trị backend từ chối | Loại `Inspection`, đổi `Urgent` thành `Critical`, đồng bộ nhãn VI/EN và màu ưu tiên với contract Application. |
| Chuyển trạng thái tài sản lặp lại trả sai HTTP | Hoàn tất work order lần hai, cho mượn tài sản đang được mượn và hoàn trả lần hai trả 409. Kiểm thử API kiểm tra chi phí, incident được giải quyết, lịch sử và mỗi audit chỉ ghi một lần. |
| E2E dùng selector/mocking lỗi thời | Scope thao tác trong đúng dialog, chọn liên kết thiết bị chính xác, mock danh sách có query phân trang. Giữ kiểm tra SignalR, chữ ký, receipt idempotency và không thay lỗi API bằng dữ liệu demo. |
| CI cấm toàn bộ tài liệu đồ án đang được Git theo dõi | Theo lựa chọn của người dùng, giữ tài liệu trong repository; CI kiểm tra nguồn website và Pages chỉ đóng gói ba tệp cho phép, từ chối symlink. README nêu rõ quyền đọc tài liệu phụ thuộc quyền truy cập repository. |

Nhận định QR `LastScannedAt` trong review cũ không được coi là lỗ hổng: trường này biểu diễn lần quét đã xác thực, không biểu diễn quyền truy cập được cấp.

## Sáu bước verification

| Bước | Kết quả | Bằng chứng |
|---|---|---|
| Build | PASS | Solution Release: 0 warning, 0 error; company và employee production build đạt. |
| Type check | PASS | Company, employee, web-ui và mobile strict TypeScript đạt. |
| Lint/format | PASS | Web/mobile ESLint không warning; whitespace format C# ở các tệp sửa trong vòng này đạt. Không tuyên bố format lại toàn repository. |
| Tests và coverage | PASS trong phạm vi cục bộ | .NET 382 đạt, 2 bỏ qua / 384; web 275/275; mobile 224/224; Python collector 20/20; dependency regression 15/15. E2E và CSP xem bên dưới. |
| Security | PASS kiểm tra có mục tiêu | Gate dependency runtime/dev đạt, không có Moderate+ chưa vá; secret-pattern scan chỉ khớp placeholder SSH trong i18n; review độc lập các thay đổi limiter, body bắt buộc, CSP và HTTP 409 không phát hiện lỗi mới trong phần đó. |
| Diff | PASS | `git diff --check` đạt; review có mục tiêu các bản sửa và contract. Không khẳng định đọc từng dòng của toàn bộ working tree có sẵn. |

Sau khi công bố metadata 409 trong OpenAPI, chạy lại 10/10 kiểm thử trọng điểm cho vòng đời tài sản, body thu hồi và tốc độ limiter đều đạt. Các kết quả này là tập con của suite, không cộng lặp vào tổng 382 test đạt. Thêm ignore cho cache tạm `.tmp-verification/`; không đưa cache hoặc log kiểm chứng vào Git.

### Độ phủ

| Phạm vi | Lines | Statements | Functions | Branches |
|---|---:|---:|---:|---:|
| Web — toàn bộ `src`, trừ type declarations | 95,48% | 92,71% | 88,91% | 87,46% |
| Mobile — toàn bộ `src`, trừ `.d.ts` | 94,03% | 91,81% | 86,29% | 83,50% |
| .NET — hợp nhất dòng được instrument, bỏ migrations/obj/bin | 81,31% (6753/8305) | Không tổng hợp | Không tổng hợp | Không tổng hợp |

Web/mobile áp dụng ngưỡng 80% cho cả bốn chỉ số, không loại màn hình sản phẩm để đạt ngưỡng. Độ phủ .NET chỉ nói về các tệp được collector instrument; không suy ra toàn bộ Worker/Setup/executable đã được đo. Agent Infrastructure riêng đạt 46,63% vì các nhánh OS/IPC thật chưa được nghiệm thu, dù tổng solution vượt 80% dòng được đo.

### Trình duyệt và gói công khai

- E2E lượt cuối sau sửa realtime và contract: **13/13 đạt**, 2,7 phút; không còn các lỗi 403 do Employee mở hub Admin. Các cảnh báo từ route mock của bộ E2E giao diện không được dùng làm bằng chứng về vận hành máy chủ thật.
- CSP production: cả company và employee đạt; nonce khác từng response, script SSR có nonce, header do client gửi không thay policy, hydration hoạt động, inline event handler bị trình duyệt chặn. `style-src 'unsafe-inline'` còn được giữ để tương thích React/Motion; production script policy không cho `unsafe-inline`/`unsafe-eval`.
- Đã parse hai workflow YAML và chạy trực tiếp các bước Bash CI/Pages: gói `dist/site` chỉ có `index.html`, `site.css`, `downloads.js`; tài liệu thiết kế trên máy vẫn tồn tại. Không thực hiện upload/deploy Pages. Giữ tài liệu trong Git không tạo tính riêng tư nếu repository công khai.

## Giới hạn còn lại

1. Hai test PostgreSQL-only bị bỏ qua vì máy không có PostgreSQL/Docker daemon hoạt động và không có connection string: rollback khi insert audit thất bại, ngân sách đăng nhập dùng chung giữa DB scopes. Không dùng InMemory làm bằng chứng tương đương. CI hiện cấu hình PostgreSQL 18.1; cần kết quả chạy thực tế trước release.
2. Chưa build/cài ứng dụng native mobile trên thiết bị thật. Expo offline dependency check chỉ xác minh các package không nằm trong danh sách exclude hiện có; không chứng minh đầy đủ tương thích RN 0.87.1 với native SDK.
3. Chưa thao tác thật khóa máy, cách ly mạng, MSI/uninstall hoặc OS IPC trên thiết bị lab được phép. Các kiểm tra fake/simulated adapter không thay thế nghiệm thu đó; cờ lab mặc định vẫn được bảo vệ.
4. Chưa đo tải NAT/nhiều replica, latency PBKDF2, query plan PostgreSQL, firewall/TLS và vận hành backup ngoài repository. Limiter hiện có ngân sách riêng mỗi process.
5. Raw `npm audit` có thể tiếp tục báo version của dependency đã backport. Kết quả gate dựa trên advisory hiện tại, checksum của từng installed copy và regression tests; cần chạy lại khi lockfile/feed thay đổi.

## Bằng chứng chạy cục bộ

Thư mục `scratch/fix-verification-2026-10-23/` bị Git ignore, chỉ dùng lưu bằng chứng trên máy:

- `dotnet-build-final.log`, `dotnet-tests-complete.log`, `dotnet-complete/**/*.trx`, `dotnet-coverage-summary.json`.
- `web-build-complete.log`, `web-coverage-complete.log`, `mobile-coverage-final.log`, `typecheck-final.log`, `lint-final.log`.
- `browser-e2e-complete.log`, `csp-browser-final.log`, `dependency-audit-final.log`, `public-package.log`.
- `contracts-final.log` xác nhận OpenAPI và hồi quy endpoint sau cập nhật metadata cuối.

Lệnh tái lập chính: `dotnet build SentinelLAN.slnx -c Release`, `dotnet test SentinelLAN.slnx -c Release --collect:"XPlat Code Coverage"`, `npm run audit:dependencies:all`, `npm run test:coverage`, `npm run test:coverage --workspace=@sentinellan/mobile -- --runInBand`, `npm run build`, `npm run test:e2e`. Để chạy gate DB bắt buộc, cấp PostgreSQL test cô lập rồi dùng `scripts/test-all.ps1 -RequirePostgres` hoặc `REQUIRE_POSTGRES=1 scripts/test-all.sh`.
