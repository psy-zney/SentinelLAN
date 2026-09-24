# Kế hoạch chi tiết luồng và tính năng SentinelLAN MVP

Cập nhật 24/09/2026. Tài liệu này mô tả vòng đời vận hành được ủy quyền của MVP, hợp đồng giữa dashboard, API và Agent, tiêu chí nghiệm thu, cùng các giới hạn cần kiểm chứng trước production. ADR 0005 ghi quyết định kỹ thuật về quản trị thiết bị và mô phỏng lệnh.

## Phạm vi và nguyên tắc

Vòng đời chính: Admin tạo tài khoản → cấp token enrollment → Agent đăng ký → heartbeat/telemetry → Admin gán Employee và policy → kỹ thuật viên xử lý cảnh báo/lệnh an toàn → Admin xem audit và thu hồi thiết bị. Mọi thao tác mang `OrganizationId`; ID trên URL không tự cấp quyền. API kiểm tra quyền và tenant, UI chỉ hiển thị thao tác phù hợp vai trò. Chỉ thu thập telemetry kỹ thuật: trạng thái, hostname, OS/Agent version, CPU, RAM, disk và thời điểm hoạt động.

Agent Core độc lập hệ điều hành. Các lệnh `SimulateLock`, `SimulateNetworkIsolation` và `RestartService` của MVP chỉ mô phỏng, không thay đổi OS/dịch vụ. Không thêm shell tùy ý, credential capture, keylogging, ảnh màn hình, tệp cá nhân, camera hay micro.

## Ma trận quyền

| Hành động | Admin | Technician | Employee | Agent |
|---|---|---|---|---|
| Danh bạ, tạo tài khoản, token enrollment | Có | Không | Không | Không |
| Inventory, dashboard, telemetry trong tenant | Có | Có | Chỉ máy được gán | Không |
| Gán/bỏ gán, thu hồi thiết bị | Có | Không | Không | Không |
| Tạo/sửa/gán policy | Có | Có | Chỉ xem policy của máy mình | Không |
| Lệnh allow-list, cảnh báo | Có | Có | Không | Poll/result cho máy mình |
| Audit tenant | Có | Không | Chỉ hoạt động liên quan máy mình | Không |
| Enrollment và heartbeat | Không bằng cookie người dùng | Không | Không | Token/credential riêng |

Các request thay đổi trạng thái qua cookie cần `X-SentinelLAN-CSRF: 1`. Mỗi hành động nhạy cảm cần lý do 3–1000 ký tự, xác nhận, tenant check và audit. Thiếu phiên trả 401, thiếu quyền 403, ID ngoài tenant trả 404, xung đột nghiệp vụ trả 409.

## F01 — Phiên người dùng và tạo tài khoản

Người dùng nhập mã tổ chức, email, mật khẩu; API xác thực trong tenant, phát cookie HttpOnly. Access token hết hạn được refresh một lần; reuse refresh token bị chặn. Logout thu hồi phiên. Admin tạo người dùng Admin/Technician/Employee với email chuẩn hóa; mật khẩu/token kích hoạt không có trong danh bạ, audit hoặc log. Checkout hiện tại có luồng kích hoạt tài khoản: liên kết chỉ hiển thị một lần, token được hash, dùng một lần và có hạn. Nếu tạo tài khoản với mật khẩu ban đầu thì password phải được hash và không trả về response.

Nghiệm thu: sai mật khẩu không tiết lộ tài khoản/tenant; Technician/Employee không tạo user; email trùng trong tenant bị chặn; email ở tenant khác không lộ dữ liệu; tài khoản chưa kích hoạt không đăng nhập; CSRF thiếu bị từ chối.

## F02 — Token enrollment và đăng ký Agent

Admin cấp token có hạn 1–60 phút, lý do và xác nhận. API sinh 32 byte ngẫu nhiên, lưu hash, trả token thô đúng một lần với `Cache-Control: no-store`; UI chỉ giữ trong bộ nhớ tới khi đóng. Agent gửi token tới `/api/v1/agent/enroll`, nhận ID và credential riêng. Giao dịch đánh dấu token đã dùng, tạo Device/credential và audit. Token sai, hết hạn, dùng lại hoặc hai lần đồng thời không được tạo hai máy.

Nghiệm thu: response/list/audit không tiết lộ hash hay token; token dùng lại trả lỗi; Agent tenant khác không thể dùng credential này; mất token phải cấp token mới.

## F03 — Thiết bị, telemetry và realtime

Agent gửi heartbeat với idempotency key; API lưu một snapshot cho mỗi key và cập nhật LastSeenAt/OS/Agent version. Dashboard và inventory hiển thị online nếu heartbeat gần đây trong 2 phút; khi timeout chuyển offline mà không làm mới LastSeenAt giả. SignalR phát sự kiện theo group tenant; client đọc lại trạng thái có thẩm quyền qua typed API. Inventory tìm theo tên/OS và lọc online, offline, revoked; detail hiển thị tối đa 100 snapshot mới nhất.

Nghiệm thu: gửi trùng key không nhân snapshot; Agent sai credential/revoked bị 401; Employee không xem máy người khác bằng ID; lỗi tải API hiển thị lỗi và Retry, không thay bằng demo data.

## F04 — Gán và thu hồi thiết bị

Admin chọn Employee cùng tenant, nhập lý do và xác nhận để gán hoặc bỏ gán. Chỉ một thiết bị chưa thu hồi được gán cho một Employee; Application kiểm tra, PostgreSQL có filtered unique index và device row version để bảo vệ race. Sau gán, Employee mới thấy máy; người cũ không còn quyền. Thu hồi giữ inventory/telemetry/audit, đánh dấu revoked, xóa assignment, thu hồi credential và phát `device-status`. Gửi lệnh hoặc heartbeat mới tới máy revoked bị chặn.

Nghiệm thu: không gán cho Admin/Technician, tenant khác hoặc máy revoked; gán song song không vượt một máy/Employee; retry thu hồi không nhân audit. Trước khi áp dụng migration trên DB có dữ liệu cũ, kiểm tra và xử lý assignment hoạt động bị trùng theo quyết định của operator, không âm thầm xóa dữ liệu.

## F05 — Policy

Admin/Technician tạo và sửa tên policy 2–100 ký tự, idle timeout 1–1440 phút, USB mode trong `Blocked`, `ReadOnly`, `FullAccess`, rồi gán cho thiết bị cùng tenant chưa thu hồi. Danh sách trả `assignedDeviceCount`. Employee thấy tên policy gán cho máy mình. Policy MVP là cấu hình; UI không tuyên bố Agent đã chặn USB hay tự khóa OS.

Nghiệm thu: tên trùng, dữ liệu sai và thiết bị ngoài tenant bị từ chối; query tên hoạt động trên PostgreSQL; số máy gán phản ánh server.

## F06 — Lệnh an toàn

Admin/Technician chọn thiết bị, loại allow-list, lý do, thời hạn và xác nhận. Server tạo ID/nonce/issued/expiry, ký HMAC gồm cả `Parameter`, lưu Pending và audit. Agent poll bằng credential riêng, xác minh thiết bị/chữ ký/thời gian/nonce, mô phỏng hành động rồi gửi receipt idempotent. Trạng thái: Pending → Delivered → Succeeded/Failed hoặc Expired. Sự kiện `command-status` cập nhật Command Center.

Nghiệm thu: không chấp nhận chữ ký sai, đổi Parameter, replay, lệnh hết hạn, sai tenant hoặc result trùng. API và Agent cần cùng định dạng chữ ký; để lệnh cũ hết hạn rồi phát lại khi nâng cấp. Cờ lab không đổi nghĩa các action `Simulate*`; `RestartService` không gọi `systemctl`.

## F07 — Cảnh báo và audit

Admin/Technician tạo cảnh báo thủ công `Info`, `Warning`, `Critical`, có thể chọn thiết bị cùng tenant; acknowledge rồi resolve. Sự kiện `alert-triggered`/`alert-updated` làm UI tải lại. Audit ghi actor, action, device, reason, outcome, UTC time; Admin lọc theo action/device. Employee chỉ xem hoạt động máy mình. Bản ghi audit append-only ở EF; chưa phải kho WORM bên ngoài.

Nghiệm thu: DeviceId tenant khác không tạo được alert hoặc lộ tên từ dữ liệu cũ sai tham chiếu; acknowledge/resolve lặp không nhân audit; lỗi API không thành danh sách rỗng; audit không chứa mật khẩu, token hoặc device secret.

## F08 — Minh bạch Employee

Employee vào `/my-device`, thấy máy được gán, telemetry được công bố, policy, sự cố/hoạt động liên quan và mô tả loại dữ liệu thu thập. Chưa có máy hoặc máy bị thu hồi hiển thị empty state rõ ràng. Employee không vào dashboard quản trị, không mở SignalR group quản trị.

Nghiệm thu: thay đổi assignment phản ánh ở phiên Employee sau khi tải lại; ID đoán không cấp quyền; lỗi API khác 404 có Retry.

## F09 — Vận hành và nghiệm thu

Thứ tự kiểm thử: tạo người dùng → kích hoạt/đăng nhập → cấp token → enroll → heartbeat → gán máy → xem My Device → policy → lệnh mô phỏng/result → cảnh báo → audit → thu hồi → Agent bị từ chối. Kiểm tra nhánh sai quyền, sai tenant, expiry, duplicate và concurrent assignment. Chạy `dotnet restore/build/test SentinelLAN.slnx`, `dotnet format SentinelLAN.slnx --verify-no-changes`, `npm.cmd run lint`, `npm.cmd run typecheck`, `npm.cmd test`, `npm.cmd run build`, `npm.cmd run test:e2e`.

MVP hiện vẫn cần kiểm chứng migration và race trên PostgreSQL thật trước production. Agent queue/nonce cache chưa bền vững qua restart; Linux credential storage, key rotation, MFA, retention tự động và external immutable audit sink là công việc tiếp theo. Cảnh báo theo ngưỡng tự động cũng chưa được nghiệm thu; cảnh báo hiện có là luồng thủ công.

## Bổ sung kiểm soát vận hành 24/09/2026

Production chỉ bootstrap tổ chức/Admin từ secret riêng, không seed demo; khóa tài khoản thu hồi phiên và vô hiệu access token. Admin/Technician phải nhập fingerprint SHA256 đã xác minh khi thêm VPS; mọi phiên SSH so pin, node cũ thiếu pin bị chặn. Restart service VPS cần lý do, xác nhận, nonce và hạn tối đa năm phút; PostgreSQL dành nonce bằng unique index trước khi gửi lệnh. QR gốc chỉ hiển thị khi phát hành, không tái tạo từ tiền tố.

Backup PostgreSQL dùng custom dump và restore drill sang database tạm; `setup-vps.sh` dùng TLS có chứng chỉ sẵn, tự tạo secret Production và kiểm tra readiness. Hàng đợi telemetry Agent có retry/idempotency nhưng vẫn chỉ ở bộ nhớ. Chưa có thử nghiệm khôi phục trên PostgreSQL/Docker thật tại máy hiện tại, chưa có MFA, luân chuyển secret tự động, audit WORM hoặc thực thi lệnh khóa/cô lập trên OS; các việc này cần thiết kế và nghiệm thu riêng trước khi tuyên bố hoàn thiện Production.

## Kết quả kiểm tra 24/09/2026

Trên checkout hiện tại: 132 test .NET trước thay đổi nonce (bao gồm kiểm tra tích hợp vòng đời quản trị thiết bị), 25 test Vitest, 6 test Playwright, web lint/typecheck/build và .NET Release build đã qua. Trên Windows, Application Control chặn tải `SentinelLAN.Infrastructure.dll` từ đường dẫn Release khi Playwright khởi động trực tiếp; chạy API Development qua `SENTINELLAN_E2E_API_COMMAND` giúp bộ test trình duyệt chạy đầy đủ. `dotnet format --verify-no-changes` còn báo lỗi newline/encoding ở migration VPS/mobile thêm sau đợt MVP. Docker CLI có nhưng daemon không chạy, nên migration/race trên PostgreSQL thật chưa được nghiệm thu. `npm audit --audit-level=high` qua với 13 cảnh báo mức moderate thuộc cây dependency Expo; NuGet audit không báo package dễ tổn thương.
