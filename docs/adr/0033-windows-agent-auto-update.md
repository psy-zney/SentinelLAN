# ADR 0033: Tự cập nhật Agent bằng tác vụ Windows riêng

Ngày: 2026-10-24. Trạng thái: chấp nhận trong mã nguồn; chờ nghiệm thu nâng cấp MSI thật trên máy lab.

## Bối cảnh

Agent chạy dưới LocalService, không đủ quyền thay phần mềm trong Program Files. Nâng toàn bộ Agent lên SYSTEM sẽ mở rộng quyền của luồng telemetry và xử lý lệnh. Bộ cài cũ chưa có cơ chế tự cập nhật.

## Quyết định

IT bật Scheduled Task SYSTEM riêng, dùng mã cố định được MSI cài trong Program Files. Tác vụ sao chép Agent self-contained sang thư mục chạy chỉ Administrators/SYSTEM được sửa, rồi chạy `--update-agent`. Tiến trình này sống độc lập với dịch vụ và không khóa binary mà MSI cần thay. Agent dịch vụ vẫn giữ LocalService.

Cấu hình updater nằm ngoài thư mục dữ liệu có quyền ghi LocalService. Nó ghim URL HTTPS manifest, khóa RSA công khai riêng cho phát hành, thumbprint Authenticode và origin công ty. Không sử dụng khóa ký lệnh để ký bản phát hành. Manifest JSON ký theo byte UTF-8, envelope gồm `payload` và `signature` base64; RSA-PSS SHA-256 tối thiểu 3072 bit. Schema 1 mang `releaseId`, `version`, `packageUrl`, `sha256`, `sizeBytes`, `publishedAt`, `expiresAt`, `companyServerUrl`, `reason`. Hạn tối đa 7 ngày; cho phép đồng hồ phát hành đi trước tối đa 5 phút. Phiên bản MSI gồm 3 thành phần, giới hạn 255.255.65535.

Không theo redirect; MSI cùng origin manifest. Tải streaming giới hạn kích thước; giữ file chỉ cho đọc trong lúc kiểm tra/cài để tránh thay file sau hash. Xác minh hash, Authenticode, chứng chỉ đã ghim và thuộc tính MSI của chính SentinelLAN. Từ chối downgrade. Ghi mã phát hành bằng CreateNew trước khi cài, không tự lặp giao dịch MSI đã bắt đầu. Khóa file độc quyền và chính sách task IgnoreNew ngăn chạy đồng thời. Không truyền tùy chọn cài đặt từ manifest.

MSI major upgrade thực hiện gỡ bản trước sau InstallInitialize để thuộc giao dịch rollback. Không ép reboot; xác minh phiên bản đăng ký và dịch vụ sau MSI. Kết quả cài và heartbeat kết nối là hai bằng chứng riêng. Chương trình ghi audit nối tiếp và trạng thái gần nhất tại máy; không thay đổi hợp đồng API hay cơ sở dữ liệu.

## Hệ quả và giới hạn

Máy cũ cần nâng cấp và IT bật tác vụ một lần. Gói unsigned hoặc chứng chỉ không được Windows tin cậy không tự cài. GitHub Releases hiện không tự tạo feed đã ký cho từng công ty; quản trị phát hành phải ký/publish theo hướng dẫn. Chỉ Windows x64 MSI được hỗ trợ. Chưa có thông báo phát hành, rollout theo nhóm từ dashboard, rollback dựa vào heartbeat hay cập nhật mobile/Linux. Không cài/gỡ phần mềm thật trên máy phát triển để coi đó là nghiệm thu. Xem `docs/guides/agent-auto-update.md` cho triển khai và gỡ bản cũ.
