# ADR 0010: Giám sát VPS theo phiên Super Admin và lệnh cố định

Ngày: 2026-09-30. Trạng thái: Đã triển khai trong mã nguồn.

## Quyết định

Portal `apps/platform` gọi API `/api/v1/platform/vps-nodes`. `PlatformOwner` được kiểm tra ở cả API và Application. VPS là tài nguyên hạ tầng chung của platform, không thuộc công ty; Admin/Technician/Employee công ty không có quyền đọc hoặc vận hành VPS.

Trình duyệt admin render bảng, biểu đồ tài nguyên, tính tuổi dữ liệu và lên lịch kiểm tra 1–5 phút (mặc định 3). Kiểm tra ngay khi mở tab Hạ tầng; dùng cùng API khi bấm kiểm tra lại. Dừng lịch khi tab bị ẩn hoặc rời màn hình; hủy request khi unmount; không chạy hai vòng cùng lúc và tối đa hai probe SSH song song. API giữ SSH key trong vault và thực hiện lệnh trên VPS. Không cài collector/worker thường trực trên VPS.

Một probe lấy CPU qua hai mẫu `/proc/stat` cách nhau một giây, RAM qua `MemAvailable`, dung lượng phân vùng `/`, uptime, trạng thái systemd và năm dịch vụ cho phép. Docker cung cấp danh sách tất cả container, health, restart policy, CPU/RAM, lớp ghi/image và network/block I/O. Docker không cài/daemon không truy cập được được báo riêng; SSH kết nối được không có nghĩa Docker hoặc ứng dụng khỏe. Container không có healthcheck được ghi rõ. CPU container có thể vượt 100% khi dùng nhiều lõi.

API trả `runtime` trong kết quả `refresh-metrics`; snapshot chi tiết chỉ giữ trong RAM trình duyệt. DB tiếp tục lưu số liệu tổng và thời gian kiểm tra hiện hữu, không thêm migration hoặc lưu toàn bộ Docker inspect. Khi lấy mẫu thành công nhưng chỉ số thiếu, xóa giá trị cũ; khi thất bại, UI đánh dấu số liệu tổng cũ và không trình bày runtime như mới.

Các thao tác chỉ nhận action/target/value thuộc allow-list, không nhận shell tự do: reboot có lịch, bật/tắt tự khởi động dịch vụ, đổi restart policy container. Tên service khớp chính xác, ID container là 64 ký tự hex, policy là `no`, `always`, `unless-stopped`. Infrastructure xây lệnh từ những giá trị này và xác minh cấu hình sau thay đổi. Bắt buộc lý do, xác nhận, nonce toàn platform chỉ dùng một lần và hạn tối đa năm phút. Ghi audit ý định trước SSH, rồi append audit kết quả. Reboot dùng `shutdown -r +1`; outcome `Scheduled` chỉ xác nhận máy chủ đã nhận lịch, không xác nhận reboot đã hoàn tất.

## Hệ quả

Giảm tải khi không có người xem. Mỗi phiên admin tự có lịch; giới hạn tải API hiện hữu vẫn áp dụng, chưa gộp probe giữa nhiều phiên. Không thu thập logs, biến môi trường, lệnh khởi động hoặc thông tin đăng nhập của container. Kết quả qua SSH có thể thay đổi trong lúc lấy mẫu; timeout hoặc mất kết nối cần kiểm tra lại trước khi phát yêu cầu mới.

VPS cần Linux, `/proc`, systemd, coreutils/`timeout` và Docker CLI nếu muốn giám sát container. Quyền Docker/systemd/sudo phải được cấp có chủ đích; thiếu quyền trả lỗi. Chính sách cập nhật trực tiếp trên container cần đồng bộ về Compose nếu container được tạo lại. Portal riêng đang được repository loại khỏi Git bằng `/apps/platform/`; thay đổi portal được giữ tại workspace riêng theo quy ước hiện hữu.

Xem [hướng dẫn vận hành VPS](../deployment/vps-operations.md).
