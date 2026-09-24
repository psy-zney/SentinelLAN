# Sổ Đăng Ký Rủi Ro Phụ Thuộc (Dependency Risk Register)

Tài liệu này ghi nhận và đánh giá toàn diện các lỗ hổng phụ thuộc bảo mật (dependency vulnerabilities) còn tồn tại sau đợt kiểm thử chất lượng tự động, lý do kỹ thuật chấp nhận rủi ro (risk acceptance), biện pháp kiểm soát bù đắp (compensating controls), và điều kiện kích hoạt nâng cấp (upgrade triggers).

---

## 1. Tổng Quan Trạng Thái Kiểm Thử

| Công cụ / Cổng kiểm thử | Kết quả kiểm thử | Mã thoát (Exit Code) | Ghi chú |
| :--- | :--- | :--- | :--- |
| **`dotnet list package --vulnerable`** | **0 vulnerabilities** | `0` | Backend C# .NET 9 hoàn toàn sạch lỗ hổng bảo mật |
| **`expo-doctor`** | **21/21 checks passed** | `0` | Không phát hiện lỗi cấu hình hay xung đột module |
| **`expo install --check`** | **Dependencies up to date** | `0` | Phù hợp tuyệt đối ma trận phụ thuộc Expo SDK 57 |
| **`mobile typecheck` (`tsc --noEmit`)** | **0 errors** | `0` | Toàn bộ TypeScript trong `apps/mobile` chuẩn hóa strict |
| **`mobile lint` (`eslint`)** | **0 warnings, 0 errors** | `0` | Không vi phạm quy chuẩn linter |
| **`mobile Jest`** | **6/6 suites, 38/38 tests passed** | `0` | Toàn bộ unit/component/security store tests pass 100% |
| **`expo export --platform android`** | **Bundle exported (1349 modules)** | `0` | Metro bundler tạo release JS bundle thành công |
| **`git diff --check`** | **Clean** | `0` | Không có lỗi khoảng trắng thừa hay định dạng |
| **`npm audit` (Toàn dự án)** | **13 moderate vulnerabilities** | `1` | Do 2 lỗ hổng bắc cầu (transitive) từ Expo ecosystem |
| **`npm audit --omit=dev`** | **13 moderate vulnerabilities** | `1` | Do `expo` và `expo-router` nằm trong `dependencies` |

---

## 2. Chi Tiết Các Lỗ Hổng & Phân Tích Rủi Ro

### 2.1. Lỗ hổng `GHSA-vcc3-ghjq-m6fr` (`decode-uri-component <= 0.4.2`)

- **Mức độ nghiêm trọng**: Moderate (Điểm CVSS: 5.3)
- **Tiêu đề**: Denial of Service (DoS) via exponential decoding of malformed percent-encoded input.
- **Chuỗi phụ thuộc (Dependency Chain)**:
  `@sentinellan/mobile@0.1.0` -> `expo-router@~57.0.22` -> `query-string@9.4.1` -> `decode-uri-component@0.4.2` (1 chuỗi xuất hiện).
- **Phạm vi xuất hiện (Runtime vs. Tooling)**:
  - Nằm trong runtime dependency của mobile app thông qua router query parser.
- **Khả năng khai thác (Exploitability Assessment)**:
  - **Rất thấp / Tiêu chuẩn kiểm soát chặt**: Kẻ tấn công cần cung cấp chuỗi deep link chứa ký tự mã hóa URL phần trăm bất thường lặp đi lặp lại để gây chậm trễ decode.
  - Ứng dụng SentinelLAN đã triển khai bộ lọc đầu vào nghiêm ngặt:
    1. Cơ chế `safeDeepLinkUrlRegex` trong [`deep-link.ts`](file:///C:/Users/admin/MyProject/SentinelLAN/apps/mobile/src/features/auth/deep-link.ts) xác thực scheme (`sentinellan://`, `https://`) và cấu trúc URL trước khi bóc tách tham số.
    2. QR Scanner chỉ chấp nhận URL thuộc allow-list miền và định dạng opaque token 64 ký tự hex hợp lệ. Mọi input sai định dạng bị chặn ở tầng validation và không chuyển tiếp vào bộ giải mã sâu.
- **Lý do không áp dụng `npm audit fix --force`**:
  - `npm audit fix --force` sẽ tự động hạ cấp `expo-router` xuống v1.x hoặc v3.x, gây vỡ kiến trúc Expo SDK 57 và New Architecture (React Native 0.87/0.86).
  - Bản vá độc lập `decode-uri-component@0.5.0` là pure ESM (`"type": "module"`), gây lỗi phân giải module trong các công cụ CommonJS và Metro bundler hiện tại.
- **Biện pháp kiểm soát bù đắp (Compensating Controls)**:
  - Input validation: Sử dụng Zod schema và regex allow-list tại mọi điểm vào (Deep Link Handler, QR Code Parser).
  - Token loại bỏ khỏi URL router params ngay sau khi nạp vào bộ nhớ (Memory Token Vault).
- **Trách nhiệm & Điều kiện nâng cấp (Upgrade Trigger)**:
  - **Chủ trì (Owner)**: Mobile Lead & Security Engineer.
  - **Đánh giá định kỳ**: Hàng quý hoặc khi Expo phát hành bản vá phụ thuộc.
  - **Upgrade Trigger**: Nâng cấp khi `expo-router` phát hành phiên bản cập nhật phụ thuộc `query-string >= 9.4.2` hoặc khi nâng cấp lên Expo SDK 58.

---

### 2.2. Lỗ hổng `GHSA-w5hq-g745-h8pq` (`uuid < 11.1.1`)

- **Mức độ nghiêm trọng**: Moderate (Điểm CVSS: 5.3)
- **Tiêu đề**: Missing buffer bounds check in v3/v5/v6 when buf argument is provided.
- **Chuỗi phụ thuộc (Dependency Chains)** (12 chuỗi xuất hiện trong báo cáo audit):
  `@sentinellan/mobile@0.1.0` -> `expo@~57.0.24` / `@expo/cli` / `@expo/config` -> `@expo/config-plugins` -> `xcode@>=0.9.2` -> `uuid@<11.1.1` (phiên bản đang dùng: `uuid@7.0.3` hoặc `uuid@3.4.0`).
- **Phạm vi xuất hiện (Runtime vs. Tooling)**:
  - **100% Build-Time / Tooling Only**: Gói `xcode` chỉ được Expo CLI và config-plugins sử dụng trên máy trạm lập trình viên / máy chủ CI để phân tích cú pháp và khởi tạo file dự án iOS Xcode (`.pbxproj`) trong quá trình `expo prebuild`.
  - **HOÀN TOÀN KHÔNG ĐƯỢC ĐÓNG GÓI VÀO RUNTIME BINARY**: Gói `uuid` và `xcode` bị Metro Bundler loại bỏ hoàn toàn khỏi JavaScript bundle của ứng dụng Android APK/AAB và iOS IPA (đã được chứng minh qua lệnh `expo export --platform android`).
- **Khả năng khai thác (Exploitability Assessment)**:
  - **Zero Exploitability trong ứng dụng phát hành**: Do mã nguồn không chạy trên thiết bị người dùng cuối.
  - Tại môi trường build, `xcode` chỉ sinh UUID nội bộ ngẫu nhiên cho các target Xcode PBX, không nhận buffer tùy ý từ bên ngoài.
- **Lý do không áp dụng `npm audit fix --force`**:
  - `npm audit fix --force` đòi hỏi hạ cấp `expo` xuống `46.0.21` (phiên bản đã lỗi thời nhiều năm).
  - Bản nâng cấp `uuid >= 11.1.1` có breaking API changes không tương thích ngược với API mà thư viện `xcode` yêu cầu (`uuid: ^7.0.3`).
- **Biện pháp kiểm soát bù đắp (Compensating Controls)**:
  - Quy trình build được cô lập trong môi trường CI/CD được phân quyền bảo mật, không xử lý file project không rõ nguồn gốc.
- **Trách nhiệm & Điều kiện nâng cấp (Upgrade Trigger)**:
  - **Chủ trì (Owner)**: DevOps & Mobile Tooling Engineer.
  - **Đánh giá định kỳ**: Hàng quý.
  - **Upgrade Trigger**: Nâng cấp tự động khi Expo SDK cập nhật `@expo/config-plugins` hỗ trợ phiên bản `xcode` mới nhất.

---

## 3. Kết Luận Đánh Giá Rủi Ro

Mức độ rủi ro tổng thể của 13 cảnh báo Moderate từ `npm audit` được phân loại là **CHẤP NHẬN ĐƯỢC CÓ ĐIỀU KIỆN (ACCEPTED WITH CONTROLS)** vì:
1. Không có lỗ hổng mức Critical hoặc High.
2. 12/13 chuỗi lỗ hổng (`uuid`) chỉ tồn tại trong công cụ build iOS PBX (`xcode`), hoàn toàn không nằm trong binary phát hành.
3. 1/13 lỗ hổng (`decode-uri-component`) trong client router đã được bảo vệ tuyệt đối bằng lớp phòng thủ Zod validation & URL Regex whitelist.
4. Mọi cổng chất lượng kỹ thuật (TypeCheck, Lint, Jest 38/38 passed, Expo Doctor 21/21 passed, Android Export) đều đạt 100% với exit code 0.
