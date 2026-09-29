# Rà soát luồng và chức năng — 2026-09-29

## Những vấn đề đã sửa

1. Cổng Chủ hệ thống (Platform Owner): trang đăng nhập riêng biệt (`/platform/login` hoặc `http://localhost:3002/platform/login`) với giao diện điều hành tối cao (Platform Control Center). Quản lý và kiểm tra chi tiết các công ty con (`/companies/{id}`: xem danh sách người dùng, thiết bị endpoint, cảnh báo đang mở, audit logs thực tế), tạm ngưng hoặc mở lại công ty, cấp lại link kích hoạt, và giám sát kết nối hạ tầng VPS & Backend (`/system/status`: tình trạng database PostgreSQL, latency, uptime, RAM working set, các node VPS SSH). Trong môi trường phát triển cục bộ, tài khoản `owner@sentinellan.local` / `PlatformOwner#2026!SecureKey` được tự động cấp kèm nút điền nhanh một chạm.
2. Gộp ứng dụng: tách `apps/platform` (app chủ), `apps/company` (app quản trị IT công ty), `apps/employee` (app web nhân viên); dùng chung thành phần qua `packages/web-ui`; cập nhật workspace, Docker, Nginx, CI và E2E.
3. Mobile IT/Admin mở màn hình nhân viên: đổi trang chủ theo quyền, IT xem thiết bị và nhận/xử lý phiếu, Admin cấp/khóa tài khoản, cấp mã enrollment và phê duyệt yêu cầu. Chặn đường dẫn nhân viên với tài khoản IT/Admin; bảo vệ API vẫn là lớp quyết định cuối cùng.
4. Điểm sức khỏe giả ở bảng thiết bị: bỏ công thức cố định `online ? 96 : 58` và `revoked ? 20`. Trang chi tiết vẫn dùng điểm do API nghiệp vụ tính, không thay bằng số tùy ý.
5. QR nhanh dùng ID thiết bị: bỏ tem không hợp lệ; chuyển tới luồng phát hành/xoay tem qua API. QR hợp lệ dùng opaque code, có thu hồi và tenant/assignment check.
6. Link sau tách app: cập nhật QR parser, URL kích hoạt, cấu hình deep link mobile và các đường dẫn liên kết chéo giữa các cổng.
7. Tài liệu chính sách/lệnh: phân biệt rõ lưu cấu hình, xếp lệnh, biên lai Agent và thao tác thật đã xác nhận.


## Ma trận luồng

| Nhóm | Dữ liệu/thao tác thực tế | Kiểm tra và giới hạn |
|---|---|---|
| Chủ hệ thống | Organization/User/activation token/nonce/audit lưu DB | Integration: tạo, kích hoạt một lần, phân quyền, CSRF, hạn/nonce, tạm ngưng/khôi phục; E2E portal riêng |
| Đăng nhập web/mobile | Hash mật khẩu, cookie hoặc bearer, refresh rotation, security stamp | Unit/integration; native SecureStore cần máy thật |
| Cấp tài khoản | Tạo pending, kích hoạt, cấp lại/thu hồi token, khóa tài khoản | Unit/integration; link phải được quản trị gửi riêng, không giả vờ đã gửi email |
| Enrollment/assignment | Token một lần, credential hash, gán/thu hồi thiết bị | Integration/E2E; tenant tạm ngưng không đăng ký hoặc gửi heartbeat được |
| Telemetry/inventory | Agent gửi CPU/RAM/disk/OS/phiên bản; SignalR cập nhật | Unit/integration/E2E; không dùng số ngẫu nhiên khi thiếu mẫu |
| Chính sách | Lưu/gán policy; `RefreshPolicy` để Agent áp dụng | Biên lai mới xác nhận kết quả; Windows cần quyền/companion |
| Lệnh thiết bị | Allow-list, chữ ký, nonce, hạn, lease, biên lai idempotent | CollectTelemetryNow thật; thiếu adapter trả Failed; lệnh `Simulate*` luôn ghi rõ mô phỏng |
| Khóa/cô lập Windows | Adapter thật có lab flag và device allow-list | Không bật tự động; nghiệm thu trên thiết bị được ủy quyền |
| ITAM/CMMS | Hồ sơ tài sản, incident, work order, loan, timeline | Typed API và Application tenant checks; kiểm thử hiện có |
| Self-service | Nhân viên báo lỗi, hẹn IT, chat, ảnh tự chọn, catalog, thông báo | Lưu DB; test từ chối thao tác sai quyền, xem trạng thái xếp hàng/thất bại/thành công riêng |
| Cài ứng dụng/bảo trì | Admin duyệt, package được phép, hash/publisher, mã một lần | Agent mới quyết định thực thi; không coi phê duyệt là cài xong |
| Mobile IT | Tổng quan công ty, thiết bị, hàng đợi, nhận/xử lý phiếu, trao đổi | Giao diện native cho tác vụ này; quản trị chi tiết còn ở web công ty |
| Mobile Admin | Tác vụ IT + cấp/khóa tài khoản, enrollment, duyệt phiếu | API Admin; chặn nhân viên/Technician ở màn hình quản lý tài khoản |
| VPS qua SSH | Host-key pin, vault mã hóa, probe và restart dịch vụ có kiểm tra | Cần SSH host thật; CPU probe chưa có mẫu trả null, không bịa phần trăm |
| Audit | Append-only ở EF; hành động có actor, lý do, thời gian | Kiểm thử; quyền DBA/WORM thuộc vận hành |
| Push | Đăng ký thiết bị, hàng đợi gửi, receipt | Cần credentials Expo và thiết bị thật; không khẳng định đã nhận push chỉ dựa vào unit test |
| VPS deployment | Ba web, API, PostgreSQL và TLS reverse proxy | Compose được cập nhật; cần Docker daemon, DNS, chứng chỉ và biến môi trường thật |

## Cách chạy

```powershell
# Terminal API — dùng môi trường Development và các bí mật riêng trong environment/user-secrets
dotnet run --project apps/backend/src/SentinelLAN.Api --no-launch-profile
# Mỗi lệnh dưới chạy ở một terminal
npm.cmd run dev:company
npm.cmd run dev:employee
npm.cmd run dev:platform
npm.cmd run dev:mobile
```

Local: công ty `http://localhost:3000/company/login`, nhân viên `http://localhost:3001/employee/login`, chủ hệ thống `http://localhost:3002/platform/login`.

Để cấp tài khoản chủ hệ thống cho API, đặt `SENTINELLAN_PLATFORM_OWNER_EMAIL` và `SENTINELLAN_PLATFORM_OWNER_PASSWORD` (ít nhất 16 ký tự, bí mật riêng), rồi khởi động API. Với Compose dùng `PLATFORM_OWNER_EMAIL` và `PLATFORM_OWNER_PASSWORD` trong file môi trường. Không có tài khoản/mật khẩu chủ hệ thống mặc định. Khi chủ hệ thống đã tồn tại, bootstrap không thay mật khẩu hoặc tạo thêm người chủ mỗi lần khởi động.

Trên VPS: mở `/platform/login`, tạo công ty, gửi link `/company/activate#token=...` cho Admin công ty. Admin kích hoạt, đăng nhập `/company/login`, cấp tài khoản IT/nhân viên và enrollment. Nhân viên dùng `/employee/login` hoặc mobile. API URL của mobile là HTTPS origin của VPS, không phải localhost trên điện thoại.

## Nghiệm thu

Kết quả kiểm thử tự động được cập nhật khi kết thúc thay đổi. Docker daemon hiện không sẵn sàng trong phiên làm việc này, vì vậy chưa xác nhận triển khai container, migration PostgreSQL thật, mạng VPS, native camera/push và hành động Windows trên máy đích. Không coi các phần chưa nghiệm thu môi trường là đã hoàn thiện vận hành; các lệnh mô phỏng vẫn được giữ rõ tên để kiểm thử an toàn theo quy tắc dự án.
