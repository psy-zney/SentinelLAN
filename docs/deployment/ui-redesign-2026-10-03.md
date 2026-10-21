> Ghi chú lịch sử; địa chỉ hạ tầng đã thay bằng ví dụ. Trạng thái hiện hành xem [tổng quan](../project-overview.md) và [giám sát host](../deployment/vps-operations.md).

# Giao diện SentinelLAN — 03/10/2026

Đã cập nhật ba giao diện web tại https://sentinel.example.com.

- Dùng nền sáng, chữ vừa phải, bảng đơn giản và nút có tên rõ việc cần làm.
- Thống nhất kiểu giao diện trong company, employee và platform qua `packages/web-ui/src/styles/app.css`.
- Menu đánh dấu trang đang mở; trên điện thoại menu cuộn ngang trong vùng riêng.
- Đổi các nhãn như Endpoint, telemetry, tenant, RBAC và nhật ký kiểm toán sang tiếng Việt dễ hiểu ở những nơi người dùng thao tác.
- Trang máy tính của nhân viên tập trung vào máy được giao. Hỗ trợ IT có trang riêng; thông số kỹ thuật và thông tin thu thập có thể mở ra để xem.
- Báo lỗi hỏi ảnh hưởng đến công việc. Các giá trị gửi cho API giữ nguyên.
- Hộp thoại quản trị hệ thống hỗ trợ bàn phím, Escape và trả lại vị trí đang chọn khi đóng. Sao chép liên kết chỉ báo thành công sau khi sao chép được.

## Kiểm tra

- `npm.cmd run lint`: đạt ở ba ứng dụng và web-ui.
- `npm.cmd run typecheck`: đạt ở ba ứng dụng và web-ui.
- `npm.cmd test`: 34/34 đạt.
- Ba bản Docker web chạy chính thức build thành công trên VPS.
- Kiểm tra trình duyệt với dữ liệu mẫu ở 1440px và 390px: menu, lọc thiết bị, thêm công ty, xác nhận trước khi gửi, lời mời, biểu mẫu SSH và câu hỏi báo lỗi đều đạt. Dữ liệu mẫu chỉ dùng trong kiểm tra, không đưa vào database.
- HTTPS: ba trang đăng nhập và `/health/ready` trả HTTP 200.
- Đăng nhập và đăng xuất thực tế với chủ hệ thống và quản trị viên công ty đạt; thông tin database vẫn kết nối được. Trang đăng nhập nhân viên đạt; luồng nhân viên được kiểm tra bằng dữ liệu mẫu cục bộ.
- Container API và PostgreSQL có cùng ID trước và sau cập nhật.

## Bản chạy

Release hiện tại: `/opt/sentinellan/releases/20261003-ui-003810`.

`/opt/sentinellan/current` trỏ đến release này. Bản `20261003-ui-003148` cập nhật cả ba ứng dụng; bản `20261003-ui-003810` chỉnh thêm bảng công ty để tên dài không làm nút xuống dòng và chỉ thay container platform. Có ảnh Docker dự phòng với thẻ `before-20261003-ui-003148` và `before-20261003-ui-003810`. Bản source trước vẫn ở `/opt/sentinellan/releases/20261002-3c38758`.

Ảnh kiểm tra nằm tại `scratch/ui-review/` trên máy phát triển. Mã nguồn platform vẫn thuộc phần riêng tư đã có của dự án.
