# ADR 0006: Kiến trúc Ứng dụng Di động và Xác thực Mobile Native

- **Trạng thái**: Chấp thuận (Accepted)
- **Ngày quyết định**: 2026-09-24
- **Tác giả**: SentinelLAN Engineering Team

## 1. Ngữ cảnh (Context)
SentinelLAN mở rộng kênh truy cập dành cho Nhân viên (Employee) thông qua ứng dụng di động Android/iOS (`apps/mobile`) để phục vụ nhu cầu:
1. Xem thông tin máy trạm được phân công (My Device) và tình trạng tuân thủ chính sách bảo mật LAN.
2. Quét tem mã QR gắn trên thân máy trạm để liên kết và xác minh thiết bị hợp lệ.
3. Báo cáo nhanh sự cố mạng/an ninh (Incident Reporting) tới đội ngũ quản trị viên SOC/IT.
4. Minh bạch dữ liệu thu thập (Privacy Manifest) để nhân viên an tâm rằng điện thoại cá nhân không bị theo dõi ngầm.

Khác với giao diện Web sử dụng `SameSite=Lax HttpOnly Cookie` và Data Protection session, ứng dụng Native Mobile hoạt động trong môi trường client không đồng nhất, yêu cầu cơ chế xác thực riêng biệt, bảo vệ token an toàn phần cứng, và không chia sẻ cơ chế cookie giữa WebView/Native.

## 2. Quyết định Kiến trúc (Decisions)

### 2.1. Nền tảng Công nghệ Mobile
- **Framework**: React Native thông qua Expo SDK 57 (Expo Router 57, React 19.2.8, React Native 0.87.1).
- **Kiến trúc Native**: New Architecture (Fabric Renderer + TurboModules) được kích hoạt mặc định (`newArchEnabled: true`).
- **Styling**: `StyleSheet.create` kết hợp Design Tokens nội bộ (`src/theme/tokens.ts`), tuân thủ tỷ lệ tương phản WCAG 2.1 AA, touch target tối thiểu >= 44dp, hỗ trợ Dark/Light Theme.
- **State Management**:
  - Server state: TanStack Query v5 (caching, query invalidation, background refetch, offline detection).
  - Form state: React Hook Form kết hợp Zod resolver.
  - Client state: `AuthContext` quản lý các trạng thái `bootstrapping`, `unauthenticated`, `authenticated`, `offline`, `session-expired`, `update-required`.

### 2.2. Chiến lược Quản lý Token và Bảo mật Native Auth
1. **Lưu trữ Token Client**:
   - **Access Token (JWT)**: Thời hạn ngắn (15 phút), **chỉ lưu trong bộ nhớ RAM (In-Memory)** của process ứng dụng. Tuyệt đối không lưu trữ xuống AsyncStorage, SQLite hay File System.
   - **Refresh Token (Opaque)**: Chuỗi ngẫu nhiên có entropy >= 256 bit (32 bytes), **chỉ lưu trong `expo-secure-store`** qua cơ chế bảo vệ của Android/iOS. Tuyệt đối không fallback về AsyncStorage không mã hóa; mức bảo vệ phần cứng phụ thuộc thiết bị.
2. **Server-Side Refresh Session & Token Family Rotation**:
   - Server chỉ lưu giá trị Hash SHA-256 của Refresh Token (`HashedToken`) và chuỗi `TokenFamily`.
   - Cơ chế xoay vòng 1 lần (Single-Use Token Rotation): Mỗi lần gọi `/api/v1/mobile/auth/refresh`, refresh token hiện tại bị đánh dấu thu hồi (`IsRevoked = true`) và một refresh token mới được cấp phát trong cùng một transaction.
   - Phát hiện sử dụng lại token (Family Reuse Detection): Nếu một refresh token đã bị thu hồi hoặc đã hết hạn được gửi lại, hệ thống lập tức thu hồi toàn bộ phiên trong cùng `TokenFamily` để bảo vệ tài khoản khỏi tấn công chiếm đoạt phiên.
   - Concurrency Control: Ngăn ngừa race condition bằng single-flight refresh lock ở phía client (`MobileApiClient`) và atomic update ở database backend.
3. **Phân biệt ranh giới Web Cookie và Mobile Bearer**:
   - Mobile endpoints được định tuyến rõ ràng tại `/api/v1/mobile/auth/*`.
   - Phản hồi từ Mobile Auth endpoints luôn mang header `Cache-Control: no-store` và được bảo vệ bởi Rate Limiter riêng biệt ("sensitive").
   - Mobile dùng `/api/v1/my-device`, `/api/v1/my-device/incidents` và `GET /api/v1/qr/{code}` với Bearer token. API cũng hỗ trợ Web Cookie theo chính sách quyền của từng endpoint.

### 2.3. Ranh giới Quyền riêng tư (Privacy Boundaries)
- Ứng dụng SentinelLAN Mobile là công cụ hỗ trợ người dùng được ủy quyền quản lý máy trạm văn phòng, **KHÔNG PHẢI phần mềm giám sát điện thoại**.
- Ứng dụng **tuyệt đối không xin cấp quyền** hoặc thu thập:
  - Vị trí địa lý / GPS.
  - Danh bạ, tin nhắn SMS, nhật ký cuộc gọi.
  - Micro / ghi âm môi trường.
  - Clipboard nền hoặc dữ liệu ứng dụng khác.
  - Camera ngầm (Camera chỉ kích hoạt khi người dùng mở màn hình Quét QR và dừng ngay lập tức khi rời màn hình hoặc ẩn ứng dụng).

### 2.4. Phân giải Mã QR và Fallback An toàn
- Chỉ chấp nhận mã định dạng tem thiết bị hợp lệ (`QR-SENTINEL-...`, UUIDv4, Base64URL token) hoặc URL tin cậy cùng domain. Từ chối thực thi bất kỳ URL bên ngoài, `javascript:`, `file:`, `data:`.
- Dừng camera khi: rời màn hình (blur), ẩn ứng dụng (background), hoặc giải mã thành công. Cơ chế debounce 2.5 giây chống quét lặp frame.
- Cung cấp phương án nhập mã tem thủ công (Manual Entry) và chọn ảnh có sẵn (Scoped Image Picker).

## 3. Hệ quả (Consequences)
- **Tích cực**:
  - JWT access token không được ghi xuống lưu trữ lâu dài; refresh token được lưu qua `expo-secure-store`.
  - Không vi phạm quyền riêng tư của nhân viên; giao diện minh bạch tạo sự tin cậy.
  - Hạn chế tối đa nguy cơ tấn công replay token hoặc IDOR nhờ phân quyền thiết bị nghiêm ngặt tại server.
- **Lưu ý / Ràng buộc**:
  - Không thể sử dụng Expo Go thuần túy cho một số native modules đặc thù trên thiết bị thật nếu cần build production release; cần sử dụng EAS Build hoặc Development Build (`npx expo run:android`).
  - Ảnh QR được chọn qua system image picker và giải mã cục bộ bằng `Camera.scanFromURLAsync`. Cần xác nhận kết quả trên Android/iOS thật; trên Android mã QR nên chiếm phần lớn ảnh.

## 4. Trạng thái triển khai cần kiểm chứng

ADR ghi quyết định kiến trúc, không tự chứng nhận bảo đảm phần cứng trên mọi điện thoại. `expo-secure-store` phụ thuộc OS/thiết bị; cần kiểm thử trên build và thiết bị đích. URL kích hoạt/QR trên web chỉ được chấp nhận khi khớp chính xác origin cấu hình trong app; HTTP chỉ được phép ở Development. Cần chạy các flow Maestro trên thiết bị thật trước khi phát hành.
