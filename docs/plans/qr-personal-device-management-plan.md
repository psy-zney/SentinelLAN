# Kế hoạch triển khai QR và quản lý thiết bị cá nhân cho SentinelLAN

Trạng thái: Ready for implementation  
Phạm vi: MVP đồ án tốt nghiệp, chạy thật trên web/PWA và Windows Agent  
Ngày lập: 2026-09-22

## 1. Mục tiêu và tiêu chí thành công

Xây dựng một luồng khép kín, có thể trình diễn bằng dữ liệu thật:

1. Admin tạo tài khoản người dùng trong cơ sở dữ liệu và cấp lời mời kích hoạt dùng đúng một lần.
2. Người dùng kích hoạt tài khoản, đăng nhập an toàn và chỉ thấy máy được gán cho mình.
3. Admin cấp enrollment token dùng đúng một lần để Agent Windows ghi danh thiết bị.
4. Hệ thống sinh nhãn QR có mã ngẫu nhiên, có thể xoay vòng hoặc thu hồi, gắn với thiết bị.
5. Người dùng dùng camera trên điện thoại hoặc laptop để quét QR. Người chưa đăng nhập chỉ thấy dữ liệu công khai tối thiểu; sau đăng nhập mới thấy dữ liệu đúng theo vai trò và tenant.
6. Admin/Technician quản lý vòng đời thiết bị, phân công, tình trạng, bảo hành, sự cố, bảo trì và nhật ký kiểm toán.
7. Employee có trang “Thiết bị của tôi” minh bạch về telemetry kỹ thuật đang được thu thập.

Tiêu chí hoàn thành:

- Luồng admin tạo user -> user kích hoạt -> đăng nhập -> xem máy cá nhân chạy end-to-end.
- Luồng cấp enrollment token -> Agent ghi danh -> thiết bị online -> gán user chạy end-to-end.
- QR được quét bằng camera thật và có phương án nhập mã/tải ảnh dự phòng.
- Token một lần không thể tái sử dụng, có hạn dùng, chỉ lưu hash và có audit.
- Không thể xem chéo tổ chức, xem thiết bị không được gán, hoặc nâng quyền bằng thay ID trên URL.
- Không thu thập nội dung cá nhân, không có shell tùy ý, keylogger, ảnh màn hình, camera/microphone nền hay thao tác phá hoại.

## 2. Đánh giá tính mới, sáng tạo và thực tế

### 2.1 Tính mới phù hợp đồ án

Không tuyên bố phát minh QR hay endpoint management. Giá trị mới nằm ở cách kết hợp có kiểm chứng:

- QR là điểm vào theo ngữ cảnh của tài sản, nhưng quyền xem vẫn do RBAC, tenant scope và assignment quyết định.
- Ba trust flow được tách riêng: tài khoản con người, ghi danh thiết bị và nhận diện nhãn QR.
- Employee có “privacy transparency view”, xem chính xác telemetry và hành động quản trị liên quan đến máy của mình.
- Một mô hình dữ liệu nối asset lifecycle, telemetry, assignment, incident, work order và audit trong modular monolith.
- Safe-command-by-design: demo thao tác nhạy cảm ở chế độ mô phỏng; không đánh đổi an toàn để tạo hiệu ứng trình diễn.

### 2.2 Điểm sáng tạo có thể trình bày trước hội đồng

- Context-aware QR: cùng một QR nhưng phản hồi khác nhau theo trạng thái đăng nhập và quyền.
- QR privacy gateway: QR chỉ chứa URL với mã opaque entropy cao, không chứa thông tin cá nhân hay secret.
- One-time activation có transaction/concurrency control, chống hai yêu cầu đồng thời cùng dùng một token.
- Privacy manifest trên trang My Device cho biết loại dữ liệu được thu thập và những loại dữ liệu cam kết không thu thập.
- Audit timeline hợp nhất các sự kiện gán máy, đổi hồ sơ tài sản, sự cố, bảo trì và hành động quản trị.
- Có thể đo lường bằng threat model, ma trận quyền, test bảo mật và số liệu thời gian hoàn tất tác vụ.

### 2.3 Tính thực tế

- Dùng PWA responsive của Next.js thay vì làm thêm ứng dụng native: dùng được camera, cài lên màn hình chính, một codebase, dễ triển khai đồ án.
- Giữ ASP.NET Core modular monolith và PostgreSQL hiện có; không đưa microservice vào khi chưa có nhu cầu vận hành.
- Agent kết nối outbound HTTPS, phù hợp NAT/firewall; không mở cổng điều khiển vào máy cá nhân.
- Ưu tiên Chrome/Edge hiện đại; camera chỉ hoạt động trên HTTPS hoặc localhost. Luôn có nhập mã và tải ảnh dự phòng.
- Tái sử dụng auth cookie, refresh rotation, SignalR và typed API client hiện có.

## 3. Vai trò và nhu cầu quản lý

### Admin

- Tạo/khóa tài khoản, chọn vai trò, gửi hoặc sao chép link kích hoạt một lần.
- Thu hồi lời mời chưa dùng; đặt lại quyền truy cập bằng token mới thay vì đọc mật khẩu cũ.
- Cấp/revoke enrollment token cho Agent, bắt buộc lý do và xác nhận.
- Gán/thu hồi máy cho user, quản lý vòng đời asset, QR, chính sách và audit.
- Xem dashboard: online/offline, chưa gán, bảo hành sắp hết, sự cố mở, token sắp/hết hạn.

### Technician

- Tra cứu/quét QR, xem thiết bị trong tenant, cập nhật hồ sơ kỹ thuật, tiếp nhận sự cố và work order.
- Không tạo Admin, không quản lý tenant, không xem secret/token, không vượt qua tenant scope.
- Chỉ phát safe command đã allow-list, có lý do, expiry, nonce, xác nhận và audit.

### Employee/chủ máy được gán

- Kích hoạt tài khoản do Admin tạo, đăng nhập/đăng xuất.
- Quét QR và xem thiết bị của chính mình; nếu QR thuộc máy khác chỉ nhận thông báo không đủ quyền.
- Xem trạng thái, thông số kỹ thuật không nhạy cảm, telemetry sức khỏe, chính sách áp dụng, lịch sử audit giới hạn.
- Báo sự cố cho máy của mình và theo dõi trạng thái xử lý.
- Xem privacy manifest và thời điểm dữ liệu gần nhất được thu thập.

### Agent

- Dùng enrollment token đúng một lần để nhận device credential riêng.
- Gửi heartbeat/telemetry kỹ thuật theo allow-list.
- Không dùng tài khoản người dùng; không nhận quyền từ QR; không thực thi shell tùy ý.

## 4. Phạm vi chức năng

### 4.1 Tài khoản do Admin tạo và kích hoạt một lần

- Admin nhập email, display name, role; backend chuẩn hóa email và chặn trùng trong tenant.
- Không cho Admin đặt hoặc nhìn thấy mật khẩu lâu dài của user.
- Backend sinh activation token ngẫu nhiên tối thiểu 256 bit; API trả plaintext đúng một lần, DB chỉ lưu SHA-256/HMAC hash.
- Link dạng `/activate?token=...`; không ghi token vào log, analytics hoặc referrer.
- User nhập mật khẩu mới và xác nhận. Chính sách tối thiểu: 12 ký tự, cho phép passphrase dài, tối đa 1024 ký tự ở request boundary.
- Token có `ExpiresAt`, `UsedAt`, `RevokedAt`, tenant/user binding và row/concurrency guard.
- Thành công thì kích hoạt user, đánh dấu token đã dùng và thu hồi các activation token còn mở của user trong cùng transaction.
- Phản hồi lỗi không tiết lộ email/tài khoản có tồn tại hay không.
- Login tiếp tục dùng HttpOnly Secure SameSite cookie, rotating refresh token và anti-CSRF hiện có.

### 4.2 Enrollment thiết bị

- Giữ enrollment token hiện có nhưng kiểm tra đầy đủ: TTL 15–60 phút, lý do, xác nhận, single-use, hash-only, rate limit và audit.
- QR enrollment dành cho quy trình cài Agent chỉ chứa server URL đã allow-list và enrollment token ngắn hạn; UI cảnh báo đây là bí mật chỉ hiển thị một lần.
- Không để trình duyệt hoặc QR public asset endpoint đổi enrollment token thành device credential.
- Agent lưu credential bằng Windows DPAPI; server chỉ lưu hash; có revoke/rotate rõ ràng.

### 4.3 QR tài sản và trình quét

- Tạo entity `DeviceQrLabel` thuộc tenant: `Id`, `OrganizationId`, `DeviceId`, `CodeHash`, `CodePrefix`, `CreatedAt`, `ExpiresAt?`, `RevokedAt?`, `LastScannedAt?`, `RowVersion`.
- Mỗi thiết bị chỉ có tối đa một QR active. Rotation thu hồi mã cũ và sinh mã mới.
- QR chứa URL canonical `/qr/<opaque-code>`; mã có entropy tối thiểu 128 bit và không tuần tự.
- Public resolve chỉ trả tên hiển thị/asset tag, trạng thái lost/retired/contact policy và không trả assigned user, serial đầy đủ, telemetry, vị trí chi tiết hay UUID nội bộ.
- Authenticated resolve trả `nextRoute` theo quyền: Admin/Technician tới device detail; Employee chỉ tới `/my-device` nếu được gán.
- Scanner PWA dùng `getUserMedia` và BarcodeDetector khi có; có fallback thư viện decode ảnh cục bộ và ô nhập mã.
- Camera chỉ khởi chạy sau thao tác người dùng, hiển thị permission state, chọn camera trước/sau, dừng stream khi rời trang.
- Chặn URL scheme/domain ngoài allow-list để QR độc hại không điều hướng người dùng.
- Rate limit resolve/scan; audit sự kiện scan đã xác thực, không lưu raw code hoặc dữ liệu camera.

### 4.4 Quản lý máy cá nhân

- Trang `/my-device` chỉ lấy dữ liệu theo actor + assignment server-side, không nhận `deviceId` từ client.
- Hiển thị: online/offline, last seen UTC có chuyển local, OS/Agent version, CPU/RAM/disk, asset profile được phép, policy, incident của chính user và 20 audit item đã lọc.
- Nút “Báo sự cố” tạo incident cho đúng máy được gán; validate title/description/severity và audit.
- Privacy card nêu dữ liệu đang thu thập và dữ liệu bị cấm.
- Không cho Employee sửa asset, gán máy, phát command, xem user khác hoặc xem cost/vendor nhạy cảm nếu không cần.

### 4.5 Bảng điều khiển quản trị

- User management: trạng thái Pending/Active/Locked, tạo invitation, revoke/reissue, lịch sử activation không chứa token.
- Device management: enrollment, assignment, QR generate/rotate/revoke/download/print, asset lifecycle.
- Operations: incidents, work orders, warranty, loan/return, telemetry health và alerts.
- Mọi mutation nhạy cảm có reason, confirmation, permission policy, tenant check và append-only audit.

## 5. Hướng giao diện

Phong cách: “calm security operations” — chuyên nghiệp, sáng rõ, ít hiệu ứng, ưu tiên độ tin cậy và khả năng đọc.

- Giữ design token Tailwind/HSL hiện có; màu trạng thái không là tín hiệu duy nhất, luôn kèm icon + text.
- Mobile-first cho scan/activate/my-device; desktop information-dense cho admin.
- Luồng scanner gồm bốn state rõ ràng: Ready, Camera permission, Scanning, Result/Error.
- QR result card hiển thị nguồn `SentinelLAN`, asset tag rút gọn và badge verified/revoked/expired.
- Form có label, help/error association, keyboard navigation, focus visible và WCAG 2.2 AA contrast.
- Token chỉ hiển thị một lần trong modal có countdown, copy button, cảnh báo không chụp/chia sẻ công khai.
- Không đưa secret vào toast, URL history dài hạn, localStorage hoặc console.
- Bổ sung tiếng Việt/Anh qua cơ chế i18n hiện có; không hard-code chuỗi mới rải rác.

Các màn hình chính:

1. `/login`: organization code, email, password; generic error; link kích hoạt nếu có token.
2. `/activate`: xác thực token, đặt mật khẩu, success -> login.
3. `/scan`: camera/file/manual code, hướng dẫn quyền camera.
4. `/qr/[code]`: public-minimal result rồi yêu cầu login hoặc deep-link theo quyền.
5. `/my-device`: overview, health, policy, incidents, privacy, audit.
6. `/users`: admin tạo tài khoản và quản lý invitation.
7. `/devices` và device detail: enrollment, assignment, QR lifecycle, asset/incident/work order.

## 6. Kiến trúc và công nghệ

Giữ stack đã pin trong ADR 0001:

- Backend: .NET 10, ASP.NET Core Minimal APIs, C# 14.
- Domain: entity/invariant thuần, không phụ thuộc EF hoặc HTTP.
- Application: contracts, validation, authorization requirements và use cases.
- Infrastructure: EF Core 10 + Npgsql/PostgreSQL 18, hashing/RNG, stores.
- API: composition root, cookie/CSRF, rate limiting, OpenAPI transport.
- Web/PWA: Next.js 16 App Router, React 19, TypeScript strict, Tailwind 4.
- QR scan: ưu tiên browser `BarcodeDetector`; fallback package nhỏ, được pin version và audit dependency.
- QR generation: SVG/canvas phía client chỉ từ canonical URL do server cung cấp; không encode object tùy ý.
- Realtime: SignalR cho device/telemetry update; không cần cho activation/QR resolve.
- Testing: xUnit, ASP.NET integration tests, Vitest và Playwright với camera permission/mock barcode.

Dependency rule:

`Domain <- Application <- Infrastructure <- Api`, trong đó Api chỉ compose; web chỉ gọi qua typed API client. QR/account/device modules trao đổi bằng Application contracts, không đọc chéo table tùy tiện.

## 7. Dữ liệu và migration

Thêm migration mới, không sửa migration đã merge:

- `User`: bổ sung trạng thái tài khoản (`PendingActivation`, `Active`, `Locked`) và security stamp/version nếu chưa có.
- `AccountActivationToken`: tenant/user, token hash unique, expiry/use/revoke timestamps, issuer, row version.
- `DeviceQrLabel`: tenant/device, code hash unique, prefix, expiry/revoke/scan timestamps, row version.
- Unique partial indexes cho một activation token active/user và một QR label active/device.
- Index luôn bắt đầu bằng `OrganizationId` đối với truy vấn tenant-owned.
- AuditLog vẫn append-only; không lưu token plaintext, password, QR raw code hoặc device secret.

Nếu thay schema/contract:

- Tạo EF migration mới.
- Cập nhật model snapshot, data dictionary, ERD, OpenAPI và seed demo an toàn.
- Seed chỉ dùng token/mật khẩu demo cố định trong Development, không áp dụng Production.

## 8. API dự kiến

### Auth và invitation

- `POST /api/v1/users`: Admin tạo pending user và nhận activation link/token đúng một lần.
- `POST /api/v1/users/{id}/activation-token`: Admin reissue, bắt buộc reason + confirmation.
- `DELETE /api/v1/users/{id}/activation-token`: Admin revoke.
- `GET /api/v1/auth/activation/validate?token=...`: trả generic validity, không lộ identity không cần thiết.
- `POST /api/v1/auth/activate`: token + new password; atomic single-use.
- Giữ `POST /auth/login|refresh|logout` và `GET /auth/session`.

### QR

- `POST /api/v1/devices/{id}/qr-label`: Admin/Technician tạo hoặc rotate có reason/confirmation.
- `DELETE /api/v1/devices/{id}/qr-label`: revoke.
- `GET /api/v1/qr/{code}/public`: minimal response, rate-limited.
- `GET /api/v1/qr/{code}`: authenticated scoped resolve, audit scan, trả route an toàn.
- Không duy trì public endpoint nhận raw device GUID sau thời gian tương thích; loại bỏ hoặc chuyển thành authenticated endpoint.

### My Device

- `GET /api/v1/my-device`: scope từ claims.
- `GET /api/v1/my-device/telemetry`: giới hạn thời gian/số điểm.
- `GET /api/v1/my-device/incidents` và `POST /api/v1/my-device/incidents`.
- Mọi endpoint có cancellation token, UTC timestamp, typed contract và ProblemDetails không lộ nội bộ.

## 9. Nội dung phải bảo mật

### Tuyệt mật, không trả lại sau khi cấp

- Password plaintext và password hash.
- Refresh token, activation token, enrollment token.
- Device credential/secret và HMAC/signing/encryption key.
- SSH private key, database credential, cookie signing key, certificate private key.
- QR opaque code dạng raw trong log/audit; audit chỉ dùng label ID hoặc prefix đã rút gọn.

### Dữ liệu cá nhân/hạn chế theo vai trò

- Email, display name, device assignment, vị trí chi tiết, serial number, purchase cost, incident notes.
- IP/network metadata nếu sau này thu thập phải có mục đích, retention và quyền xem riêng; MVP không cần lưu lịch sử duyệt web hay traffic payload.
- Telemetry lịch sử phải có retention policy và tenant isolation.

### Không được thu thập hoặc xây dựng

- Keystroke, clipboard, screen capture, webcam/microphone, nội dung file, credential trình duyệt, lịch sử duyệt web.
- Arbitrary shell/PowerShell, persistence ẩn, bypass antivirus, covert monitoring.
- Real lock/network isolation ngoài lab flag, test device được phép và adapter được review riêng.

### Kiểm soát bắt buộc

- TLS production, HSTS, Secure/HttpOnly/SameSite cookies, credentialed CORS allow-list và CSRF header.
- Password hash PBKDF2 hiện có được version hóa/nâng chi phí; cân nhắc Argon2id trong ADR tương lai.
- RNG cryptographic; constant-time compare; token hash có domain separation.
- Rate limit login, activation, enrollment và QR resolve; generic errors chống enumeration.
- Không cache response chứa token/PII; `Cache-Control: no-store`; Referrer-Policy phù hợp.
- CSP giới hạn camera cho chính origin; permission policy `camera=(self)` ở scanner.
- Append-only audit, UTC, actor, target, reason, outcome; không cho update/delete qua app.
- Secret chỉ từ environment/user-secrets; không commit `.env`, certificate, dump hay log.
- Supply-chain audit cho NuGet/npm và pin dependency QR fallback.

## 10. Threat model tối thiểu

| Mối đe dọa | Kiểm soát | Kiểm thử |
|---|---|---|
| Đoán/quét mã QR | code entropy cao, rate limit, public data tối thiểu | invalid/high-volume resolve |
| QR bị chụp lại | revoke/rotate, không chứa secret lâu dài | old code returns gone/not found |
| Dùng activation token hai lần | transaction + row version + UsedAt | hai request đồng thời, chỉ một thành công |
| Cross-tenant IDOR | tenant scope ở Application/store | tenant A không xem/sửa tenant B |
| Employee đổi URL để xem máy khác | scope bằng claims/assignment | 403/404 nhất quán |
| Token lọt vào log/referrer | redaction, no-store, referrer policy | log assertions/header tests |
| CSRF mutation | anti-CSRF header + SameSite | request thiếu header bị chặn |
| XSS lấy token | HttpOnly cookie, output encoding, CSP | token không có trong DOM/storage |
| Agent giả mạo | credential riêng, hash-only, revoke | invalid/revoked credential rejected |
| Replay command | nonce, expiry, signature, idempotency | expired/replayed result rejected |

## 11. Kế hoạch thực hiện theo pha

### Pha 0 — Baseline và bảo toàn thay đổi hiện có

- Ghi nhận `git status`, không reset/ghi đè thay đổi người dùng.
- Chạy focused baseline cho auth, enrollment, device, web typecheck/test.
- Đối chiếu QR/ITAM code đang dở với kế hoạch; tái sử dụng phần đúng, refactor phần vi phạm boundary.

### Pha 1 — Domain/Application và test trước

- Thêm account status, activation token, QR label entity và invariant.
- Thêm contracts/services/stores theo Application boundary.
- Unit test expiry, single-use, revoke, rotation, permission, assignment và concurrency.

### Pha 2 — Persistence/API

- EF mapping/index/migration mới và PostgreSQL-safe concurrency.
- API activation, QR public/authenticated resolve, QR lifecycle và my-device incident.
- Rate limit, no-store, generic ProblemDetails, CSRF/auth policy và append-only audit.
- Integration test happy path + authorization/failure path.

### Pha 3 — Web/PWA

- Typed API types/client trước, sau đó UI activate/scan/QR/user/device/my-device.
- Camera lifecycle, fallback upload/manual input, accessible state/error UI.
- i18n Việt/Anh và responsive layout.
- Không đặt token/secret trong localStorage, analytics hoặc console.

### Pha 4 — Agent và luồng thật

- Xác minh Agent enrollment hiện có dùng single-use token và DPAPI thật trên Windows.
- Không mở rộng telemetry ngoài privacy allow-list.
- Dùng SignalR/heartbeat để trang quản trị và My Device phản ánh dữ liệu thật.

### Pha 5 — Tài liệu và kiểm chứng

- Cập nhật OpenAPI, ERD/data dictionary, use cases, threat model, ADR và demo script.
- Viết kịch bản demo từ zero state, gồm cả lỗi token reuse/cross-tenant.
- Chạy format, restore, build, toàn bộ .NET test, lint, typecheck, Vitest, Next build và Playwright phù hợp.
- Chạy `dotnet list package --vulnerable` và `npm audit` ở mức báo cáo; không nâng major ngoài kế hoạch.

## 12. Test matrix và acceptance criteria

### Backend

- Admin tạo invitation; Technician/Employee bị từ chối.
- Token hash-only, expiry, revoke, used-once và race condition đúng.
- Login user pending/locked bị từ chối; active user đăng nhập được.
- QR public không lộ PII/UUID/telemetry; old/revoked code không dùng được.
- QR authenticated resolve đúng role/tenant/assignment.
- Mọi mutation có audit; audit không chứa secret.
- Enrollment single-use, tenant isolation và device revoke vẫn pass.

### Web

- Scanner render tốt khi không có camera/BarcodeDetector.
- Permission denied đưa ra hướng dẫn và manual fallback.
- Stream camera được stop khi unmount.
- URL không thuộc origin/format cho phép không được điều hướng.
- Activate form, generic error, loading/double-submit guard, accessible focus.
- Employee không nhìn thấy admin action; admin thao tác QR/invitation được.

### End-to-end

1. Admin login -> tạo employee -> copy activation link.
2. Employee activate -> login -> chưa có máy.
3. Admin cấp enrollment token -> Agent enroll -> device online.
4. Admin gán device -> generate QR.
5. Employee scan -> được chuyển tới My Device và tạo incident.
6. Technician nhận incident/work order; timeline/audit cập nhật.
7. Rotate QR -> QR cũ thất bại, QR mới hoạt động.
8. Tenant khác và employee khác không thể truy cập.

## 13. Chỉ số đánh giá học thuật và vận hành

- Security correctness: 100% test tenant/role/token/replay quan trọng pass.
- Task success: ít nhất 90% người thử hoàn tất scan -> xem thiết bị không cần hướng dẫn trực tiếp.
- Efficiency: median dưới 60 giây cho scan và báo sự cố; dưới 3 phút cho admin tạo user + gán máy.
- Reliability: không tạo trùng activation/enrollment khi gửi đồng thời; API idempotent ở điểm cần thiết.
- Privacy: data inventory khớp schema/API/Agent; không có trường dữ liệu bị cấm.
- Accessibility: keyboard-only và screen-reader labels cho các luồng chính; contrast WCAG 2.2 AA.
- Performance mục tiêu: API resolve p95 dưới 300 ms trong môi trường demo LAN; scanner first interaction dưới 2 giây sau cấp quyền trên thiết bị thử nghiệm.
- Báo cáo đồ án cần mô tả phương pháp test, cấu hình máy, kích thước mẫu và giới hạn; không khẳng định vượt quá dữ liệu đo được.

## 14. Ngoài phạm vi MVP

- Mobile native iOS/Android riêng.
- Facial recognition, GPS tracking, background camera hoặc biometric identity.
- MDM đầy đủ, remote desktop, arbitrary command execution.
- MFA/passkey, asymmetric device attestation và external immutable SIEM là hướng phát triển sau; không giả vờ đã hoàn tất.
- Public internet discovery/search của thiết bị.

## 15. Definition of Done

- Code tuân thủ boundary Domain/Application/Infrastructure/Api và web typed client.
- Migration mới, OpenAPI và tài liệu đồng bộ.
- Không có secret/PII mới bị commit hoặc log.
- Relevant tests pass, không làm hỏng auth/enrollment/device hiện có.
- QR camera + fallback và toàn bộ account activation/personal device flow chạy thật.
- Safe simulation vẫn là mặc định; mọi hành vi thật được mô tả trung thực.
- Báo cáo cuối nêu file thay đổi, lệnh kiểm thử/kết quả, phần mô phỏng so với thật và rủi ro còn lại.
