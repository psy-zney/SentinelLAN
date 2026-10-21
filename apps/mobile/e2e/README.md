# Kiểm thử mobile trên thiết bị

Các flow Maestro dùng app ID `com.sentinellan.employee`. Cần Android emulator hoặc thiết bị Android có `adb`, Maestro CLI, ứng dụng đã cài, và API thử nghiệm truy cập được từ thiết bị. Chạy luồng trên tenant/thiết bị thử được ủy quyền. Flow `00` kích hoạt tài khoản Employee mới bằng `MAESTRO_ACTIVATION_LINK` một lần; nếu tài khoản đã kích hoạt, bắt đầu từ `01`. Link HTTPS chỉ mở app khi domain đã phục vụ `/.well-known/assetlinks.json` (Android) và `/.well-known/apple-app-site-association` (iOS) đúng app ID và chứng chỉ ký. Scheme `sentinellan://activate?token=...` dùng để thử route app trước khi cấu hình liên kết domain.

Trước khi chạy, cấp các biến môi trường `MAESTRO_ORG_CODE`, `MAESTRO_EMPLOYEE_EMAIL`, `MAESTRO_EMPLOYEE_PASSWORD`, `MAESTRO_ASSIGNED_QR_CODE` và `MAESTRO_FORBIDDEN_QR_CODE` từ fixture thử nghiệm. Flow `00` cần thêm `MAESTRO_ACTIVATION_LINK` chứa token mới; mật khẩu đó phải trùng `MAESTRO_EMPLOYEE_PASSWORD` dùng ở `01`. Mã QR bị từ chối phải là tem **có thật** của thiết bị không được gán cho Employee đang đăng nhập; mã ngẫu nhiên chỉ kiểm tra nhánh 404. Không ghi mật khẩu hoặc mã tem vào file repo hay dòng lệnh lưu trong lịch sử shell.

Chạy flow theo thứ tự số để dùng chung phiên đăng nhập:

```text
maestro test apps/mobile/e2e/00-activate-account.yaml # chỉ khi có tài khoản/token mới
maestro test apps/mobile/e2e/01-login.yaml
maestro test apps/mobile/e2e/02-scan-qr-my-device.yaml
maestro test apps/mobile/e2e/03-report-incident.yaml
maestro test apps/mobile/e2e/05-forbidden-qr.yaml
maestro test apps/mobile/e2e/06-deny-camera-fallback.yaml
maestro test apps/mobile/e2e/07-cold-start-offline.yaml
maestro test apps/mobile/e2e/04-cold-start-refresh-and-logout.yaml
```

Flow `06` đặt quyền camera thành `deny` rồi kiểm tra nhập mã thủ công. Flow `07` dùng airplane mode của Android, xác nhận app giữ refresh token mà không hiển thị dữ liệu riêng khi chưa xác thực lại; sau khi bật mạng, nút Thử lại phải khôi phục phiên. Flow `04` đăng xuất nên chạy cuối. Chọn ảnh QR được kiểm tra bằng test component; để xác nhận native, cần chuẩn bị ảnh QR thử trong thư viện ảnh của thiết bị và thử quét bằng tay trên Android/iOS.

Các lệnh `npm run lint:mobile`, `npm run typecheck:mobile`, `npm run test:mobile` và `expo export` kiểm tra mã và bundle JavaScript. Chúng không thay thế build APK/IPA và chạy Maestro trên OS thật. Với EAS preview/production, cấu hình `EXPO_PUBLIC_API_URL` là HTTPS origin của API và `EXPO_PUBLIC_WEB_URL` là HTTPS origin của link kích hoạt/QR (nếu khác API). Liên kết EAS project bằng tài khoản của tổ chức trước khi build; không dùng project ID giả hoặc domain `.local` cho bản phát hành công khai.
