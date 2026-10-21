# Sổ theo dõi rủi ro phụ thuộc

Đây là hồ sơ cho các cảnh báo từng xuất hiện trong quá trình phát triển mobile, **không phải kết quả audit hiện tại**. Mọi quyết định chấp nhận rủi ro cần được xác nhận lại với lockfile và kết quả quét của commit sẽ triển khai. Giữ mã advisory để đối chiếu; không suy ra mức độ khai thác chỉ từ việc package nằm trong dependency tree.

| Advisory từng được ghi nhận | Đường phụ thuộc cần kiểm tra lại | Việc cần làm |
|---|---|---|
| `GHSA-vcc3-ghjq-m6fr` (`decode-uri-component`) | `expo-router` → `query-string` | Kiểm tra phiên bản thực tế, đường deep link/QR và bản vá tương thích Expo. Không coi input validation là bằng chứng loại bỏ toàn bộ rủi ro. |
| `GHSA-w5hq-g745-h8pq` (`uuid`) | Expo tooling → `xcode` | Kiểm tra package có trong build/runtime nào và phạm vi input mà tooling xử lý. Không khẳng định không thể khai thác khi chưa kiểm chứng artifact. |

Trước mỗi đợt phát hành, chạy `npm audit --omit=dev` và `npm audit` ở gốc repo, cùng lệnh kiểm tra NuGet vulnerable theo SDK đang dùng. Lưu ngày, commit, lockfile, bản báo cáo và quyết định của người chịu trách nhiệm trong hệ thống theo dõi phát hành. Nếu vẫn còn cảnh báo, ghi phiên bản phụ thuộc, đường khai thác khả dĩ, biện pháp giảm thiểu, hạn xem lại và điều kiện nâng cấp. Không chạy `npm audit fix --force` tự động khi chưa kiểm tra khả năng tương thích và test lại.
