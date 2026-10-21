# Nâng cấp sản phẩm SentinelLAN — 2026-10

Phạm vi được yêu cầu: triển khai toàn bộ lộ trình. Mã nguồn và kiểm thử quyết định trạng thái hoàn thành; không coi ước lượng số ngày hoặc điểm bảo vệ là cam kết.

## Theo dõi triển khai

- [x] Mốc 1: bỏ scratch khỏi bản nguồn, tách báo cáo, làm sạch collector/IP, sửa forwarded headers và metadata ảnh.
- [x] Inventory tự động Windows/Linux: serial, hãng, model, CPU, RAM, đĩa và IP/MAC LAN; giới hạn dữ liệu và xử lý thiếu quyền.
- [x] Trạng thái bảo vệ endpoint: antivirus, ba profile firewall và mã hóa đĩa; trạng thái chưa xác định không bị coi là tắt; alert/security event có chống trùng.
- [x] Cấu hình cảnh báo/bảo trì theo tổ chức, quyền Admin, validation, xác nhận/lý do và audit; giao diện quản trị.
- [x] Phiếu bảo trì Preventive khi sắp hết bảo hành/đến chu kỳ; chạy nền idempotent, không tạo lệnh điều khiển máy.
- [x] Tuân thủ phần mềm: ProductCode/UpgradeCode đã cấu hình, phiên bản, phần mềm cấm; không gửi toàn bộ phần mềm cá nhân hoặc dữ liệu sử dụng.
- [x] Phòng ban cho người dùng/thiết bị; gán policy theo phòng ban và chọn nhiều máy cho RefreshPolicy/CollectTelemetryNow.
- [x] Enrollment QR và script cài nhanh có kiểm chứng gói cài; mỗi máy dùng mã riêng, hết hạn 15 phút; luồng batch có audit; mobile xem trước mã đúng máy chủ.
- [x] Xác thực HMAC request: key chuyên dụng được bảo vệ khi lưu, timestamp + nonce dùng một lần, giới hạn body, chuyển đổi credential cũ; không gửi DeviceSecret trong mỗi heartbeat/poll.
- [x] SignalR wake-up cho Agent, HTTP poll/lease vẫn bảo đảm khôi phục; thu hồi quyền và reconnect; không truyền nội dung lệnh qua tín hiệu wake-up.
- [x] Tùy chọn Redis backplane và S3/MinIO với mã hóa ảnh; mặc định vẫn chạy một node và lưu DB; kiểm tra tenant khi tải ảnh.
- [x] Adapter Linux có kiểm tra quyền/allow-list, khóa/cô lập chỉ trên thiết bị lab được ủy quyền và có khôi phục; không thực thi lệnh tùy ý.
- [x] Tài liệu kiến trúc, OpenAPI, schema/migrations, riêng tư, demo và bản thảo khóa luận đồng bộ với tính năng đã triển khai; phân biệt giới hạn kiểm chứng.

Dấu hoàn thành thể hiện đã triển khai trong source; nghiệm thu môi trường thật và các giới hạn kiểm tra cuối được ghi riêng bên dưới. Hướng dẫn cấu hình tại [product-upgrade](../guides/product-upgrade.md), hợp đồng tại [API](../api/product-upgrade.md), quyết định kiến trúc tại [ADR 0030](../adr/0030-product-upgrade-and-durable-signals.md).

## Ràng buộc kiểm chứng

Mỗi thay đổi có kiểm thử đúng/sai quyền, khác tenant, dữ liệu không hợp lệ và retry phù hợp. Đánh giá Windows WMI/Defender/BitLocker, Linux systemd/nftables/loginctl, Redis/S3 và UI trên môi trường thật phải được ghi riêng; không suy từ unit test thành nghiệm thu thực địa. Mọi thao tác ảnh hưởng OS giữ cấu hình an toàn mặc định.

### Kết quả kiểm tra ngày 21–22/10/2026

| Kiểm tra | Kết quả và phạm vi |
|---|---|
| Build .NET toàn solution sau thay đổi cuối | Qua, 0 lỗi và 0 cảnh báo |
| Agent | 94/94 qua sau build cuối; fixture DPAPI/ACL chạy ngoài sandbox; Windows/Linux command dùng platform giả |
| Backend ở các đợt trước | Application 65, Domain 23, Architecture 1 và Integration 161 đã qua; các sửa hồi quy có chạy kiểm tra liên quan |
| Backend ở lần chạy cuối | Chưa xác nhận lại toàn bộ: Windows Code Integrity chặn nạp `SentinelLAN.Domain.dll` với `0x800711C7`, sự kiện 3077/3033; build lại vẫn bị chặn. Architecture 1 qua; không tính các bài lỗi nạp DLL là kiểm thử chức năng đạt |
| Web | 44 test qua; lint/typecheck qua; company và employee production build qua |
| Mobile | 69 test/11 suite qua; lint/typecheck qua; gồm parser QR enrollment và QR thiết bị |
| Collector/hạ tầng Python | 20 test qua |
| Dependency security | NuGet không có project bị advisory trong kết quả audit; 12 test backport npm qua; npm audit không còn high/critical chưa vá; còn moderate ở decode-uri-component, sprintf-js, uuid |
| Database | Model/migration đã đối chiếu, SQL idempotent đã xuất; chưa áp dụng/restore trên PostgreSQL thật vì Docker engine không khả dụng |
| Trình duyệt | Đã thử đăng nhập/lưu ngưỡng CPU 20%, xem panel phòng ban/enrollment/bulk và máy demo. Kiểm tra lại bố cục sau CSS cuối bị chặn do quyền local browser trong Work đã tắt |
| Khóa luận | Bản thảo Markdown/DOCX riêng đã đồng bộ; render 87 trang, kiểm tra ảnh và sửa trang trắng thừa. Còn chỗ dành cho thông tin tác giả, hình và số liệu thực nghiệm phải điền bằng bằng chứng thật |
| Cấu hình và script | Ba cấu hình Compose hợp lệ; PowerShell/Bash bootstrap qua kiểm tra cú pháp; liên kết tài liệu cục bộ và `git diff --check` qua |

Log và ảnh kiểm tra nằm trong `test-results/` được ignore. Cần chạy lại `dotnet test SentinelLAN.slnx` trên máy/CI được phép nạp assembly do dự án build; không tắt chính sách bảo mật để coi kiểm thử là đạt.

### Nghiệm thu vận hành còn cần môi trường đích

- [ ] PostgreSQL nâng cấp từ dữ liệu cũ và backup/restore; backup trước khi migration xóa bảng VPS cũ, Down không khôi phục dữ liệu đã xóa.
- [ ] Windows cài sạch, CIM/WMI, antivirus/firewall/BitLocker thực; Linux systemd/loginctl/nftables và rollback trên máy lab được ủy quyền.
- [ ] Redis nhiều instance/failover, MinIO/S3 TLS và restore cả DB/bucket/khóa; điện thoại thật nhận push và quét enrollment.
- [ ] Đo độ trễ wake-up, tải đồng thời, kiểm tra giao diện cuối và bổ sung hình/số liệu báo cáo. Chưa cam kết độ trễ dưới một giây hoặc hiệu năng production.

## Cơ sở trước nâng cấp

Mốc 1 đã qua 140 integration test, 65 Application test, 20 Python test và kiểm tra Compose/model. Các thay đổi này đang ở worktree, chưa tạo commit. Báo cáo riêng được bảo toàn tại thư mục SentinelLAN-khoa-luan cạnh repo; lịch sử Git chưa được viết lại.
