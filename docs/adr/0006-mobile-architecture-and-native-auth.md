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
- **Framework**: React Native thông qua Expo SDK 57 (Expo Router v54, React 19.2.8, React Native 0.86.0).
- **Kiến trúc Native**: New Architecture (Fabric Renderer + TurboModules) được kích hoạt mặc định (`newArchEnabled: true`).
- **Styling**: `StyleSheet.create` kết hợp Design Tokens nội bộ (`src/theme/tokens.ts`), tuân thủ tỷ lệ tương phản WCAG 2.1 AA, touch target tối thiểu >= 44dp, hỗ trợ Dark/Light Theme.
- **State Management**:
  - Server state: TanStack Query v5 (caching, query invalidation, background refetch, offline detection).
  - Form state: React Hook Form kết hợp Zod resolver.
  - Client state: `AuthContext` quản lý finite state machine (`bootstrapping`, `unauthenticated`, `authenticated`, `session_expired`, `update_required`).

### 2.2. Chiến lược Quản lý Token và Bảo mật Native Auth
1. **Lưu trữ Token Client**:
   - **Access Token (JWT)**: Thời hạn ngắn (15 phút), **chỉ lưu trong bộ nhớ RAM (In-Memory)** của process ứng dụng. Tuyệt đối không lưu trữ xuống AsyncStorage, SQLite hay File System.
   - **Refresh Token (Opaque)**: Chuỗi ngẫu nhiên cryptographically secure entropy >= 256 bit (32 bytes), **chỉ lưu trong `expo-secure-store`** (mã hóa phần cứng Android Keystore / iOS Keychain). Tuyệt đối không fallback về AsyncStorage không mã hóa.
2. **Server-Side Refresh Session & Token Family Rotation**:
   - Server chỉ lưu giá trị Hash SHA-256 của Refresh Token (`HashedToken`) và chuỗi `TokenFamily`.
   - Cơ chế xoay vòng 1 lần (Single-Use Token Rotation): Mỗi lần gọi `/api/v1/mobile/auth/refresh`, refresh token hiện tại bị đánh dấu thu hồi (`IsRevoked = true`) và một refresh token mới được cấp phát trong cùng một transaction.
   - Phát hiện sử dụng lại token (Family Reuse Detection): Nếu một refresh token đã bị thu hồi hoặc đã hết hạn được gửi lại, hệ thống lập tức thu hồi toàn bộ phiên trong cùng `TokenFamily` để bảo vệ tài khoản khỏi tấn công chiếm đoạt phiên.
   - Concurrency Control: Ngăn ngừa race condition bằng single-flight refresh lock ở phía client (`MobileApiClient`) và atomic update ở database backend.
3. **Phân biệt ranh giới Web Cookie và Mobile Bearer**:
   - Mobile endpoints được định tuyến rõ ràng tại `/api/v1/mobile/auth/*`.
   - Phản hồi từ Mobile Auth endpoints luôn mang header `Cache-Control: no-store` và được bảo vệ bởi Rate Limiter riêng biệt ("sensitive").
   - Các API chia sẻ dữ liệu (`/api/v1/my-device`, `/api/v1/qr/resolve`, `/api/v1/incidents`) chấp nhận đồng thời Web Cookie hoặc Mobile Bearer Token thông qua OpenApi Bearer Security Scheme.

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
  - Bảo mật tối đa: Mất điện thoại hay bị trích xuất file system cũng không rò rỉ JWT access token; refresh token được bảo vệ bởi hardware keystore.
  - Không vi phạm quyền riêng tư của nhân viên; giao diện minh bạch tạo sự tin cậy.
  - Hạn chế tối đa nguy cơ tấn công replay token hoặc IDOR nhờ phân quyền thiết bị nghiêm ngặt tại server.
- **Lưu ý / Ràng buộc**:
  - Không thể sử dụng Expo Go thuần túy cho một số native modules đặc thù trên thiết bị thật nếu cần build production release; cần sử dụng EAS Build hoặc Development Build (`npx expo run:android`).
  - Thư viện giải mã QR offline từ ảnh tĩnh trong New Architecture (Fabric) cần native module chuyên biệt; ở giai đoạn hiện tại cung cấp UX minh bạch và hướng dẫn người dùng quét trực tiếp hoặc nhập tem thủ công.
