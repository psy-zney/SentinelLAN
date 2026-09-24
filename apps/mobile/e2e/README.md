# SentinelLAN Mobile - Maestro E2E Testing Guide

Tài liệu này định nghĩa và hướng dẫn thực thi bộ kiểm thử E2E (End-to-End) tự động cho ứng dụng **SentinelLAN Mobile** (`@sentinellan/mobile`, `com.sentinellan.mobile`) bằng [Maestro](https://maestro.mobile.dev/).

## 1. Yêu cầu môi trường chạy E2E
Để thực thi Maestro E2E trên máy cục bộ hoặc CI:
- **Maestro CLI**: Đã cài đặt (`curl -Ls "https://get.maestro.mobile.dev" | bash` hoặc trên Windows qua Scoop / binary `maestro.exe`).
- **Android Emulator hoặc Thiết bị thật**:
  - Android 10+ (API level 29 trở lên).
  - Đã bật Developer Options & USB Debugging.
  - `adb devices` hiển thị ít nhất một thiết bị `device`.
- **Bản dựng ứng dụng**:
  - Development APK hoặc Preview APK tạo bởi `eas build --platform android --profile preview` hoặc `npx expo run:android`.
  - Package ID: `com.sentinellan.mobile`.
- **Backend API**:
  - Đang chạy ASP.NET Core API tại máy chủ LAN hoặc HTTPS hợp lệ (ví dụ: `https://sentinellan.local/api/v1`).

## 2. Danh mục E2E Flows
- **`01-activate-and-login.yaml`**: Luồng khởi động ứng dụng lần đầu, kích hoạt / nhập thông tin đăng nhập native và xác thực thành công.
- **`02-scan-qr-my-device.yaml`**: Luồng quét mã QR tem máy trạm (bao gồm fallback nhập mã thủ công), phân giải thiết bị qua API và xem trang "Máy của tôi" kèm Cam kết minh bạch dữ liệu.
- **`03-report-incident.yaml`**: Luồng tạo báo cáo sự cố an ninh có idempotency key, gửi lên server và hiển thị trong danh sách sự cố.
- **`04-cold-start-refresh-and-logout.yaml`**: Luồng đóng ứng dụng hoàn toàn (cold-start), khôi phục phiên từ SecureStore qua refresh token rotation, và thực hiện đăng xuất an toàn (thu hồi refresh session trên server và xóa SecureStore cục bộ).
- **`05-forbidden-qr.yaml`**: Luồng quét / nhập mã thiết bị không thuộc về tài khoản nhân viên; hệ thống hiển thị thông báo từ chối truy cập và ngăn chặn chuyển hướng.
- **`06-deny-camera-fallback.yaml`**: Luồng từ chối cấp quyền camera; ứng dụng hiển thị rationale, cho phép chọn ảnh từ thư viện với safe UX disclosure (không giả mạo thành công) và chuyển tiếp nhập tem thủ công an toàn.
- **`07-cold-start-offline.yaml`**: Luồng khởi động ứng dụng trong tình trạng ngắt kết nối mạng; kiểm tra khả năng phục hồi phiên và hiển thị cảnh báo offline.

## 3. Lệnh thực thi
```bash
# Chạy toàn bộ flows theo thứ tự:
maestro test apps/mobile/e2e/01-activate-and-login.yaml
maestro test apps/mobile/e2e/02-scan-qr-my-device.yaml
maestro test apps/mobile/e2e/03-report-incident.yaml
maestro test apps/mobile/e2e/04-cold-start-refresh-and-logout.yaml
maestro test apps/mobile/e2e/05-forbidden-qr.yaml
maestro test apps/mobile/e2e/06-deny-camera-fallback.yaml
maestro test apps/mobile/e2e/07-cold-start-offline.yaml

# Hoặc chạy toàn bộ thư mục:
maestro test apps/mobile/e2e/
```

## 4. Trạng thái hiện tại trong môi trường cục bộ
- **Tooling check**: Môi trường hiện tại không có sẵn `adb` và Android SDK Emulator trên PATH. Do đó, các flows này được lưu trữ chuẩn hóa dưới dạng mã nguồn có thể thực thi ngay khi kết nối thiết bị hoặc chạy trên CI/CD runner có Android SDK.

