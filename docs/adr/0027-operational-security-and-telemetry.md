# ADR 0027: Phiên bảo mật, thời gian telemetry và biên lai gửi bù

## Quyết định

Refresh session giữ security stamp lúc cấp; đổi mật khẩu thu hồi các phiên và thay stamp. Session không có stamp phải đăng nhập lại. SignalR kiểm tra lại quyền sau thao tác thay đổi tài khoản và theo chu kỳ, đồng thời đóng khi hết hạn xác thực.

Telemetry giữ giờ thu thập nullable để tương thích Agent cũ, dùng `CreatedAt` trong database làm giờ nhận. DTO thêm `CollectedAt` và `ReceivedAt`; trường compatibility `CreatedAt` lấy giờ đo khi có. Lịch sử sắp xếp theo giờ đo. Cơ chế lưu giữ chỉ xóa telemetry và heartbeat quá 30 ngày, không xóa audit.

Hạn chữ ký quyết định lúc bắt đầu thực thi. Hạn nhận biên lai kéo dài thêm 7 ngày để kết quả đã thực hiện có thể gửi bù. Trạng thái `ExecutionUnconfirmed` phân biệt lệnh đã giao thiếu xác nhận với lệnh hết hạn chưa giao. Hạn nhận biên lai không cho phép chạy lại lệnh.

Cảnh báo tự động có ngưỡng và khoảng thời gian rõ ràng, chống trùng qua unique index và cooldown, tự đóng khi có dữ liệu phục hồi, đồng thời ghi audit. Các worker dùng Application use cases và Infrastructure ports.

## Hệ quả

Triển khai cần migration `OperationalSecurityAndTelemetry`. Refresh token trước migration không thể gia hạn; người dùng đăng nhập lại. Agent và API được nâng cấp độc lập nhờ trường thời gian tùy chọn. Hệ thống vẫn có khoảng hở giữa ghi nonce và ghi biên lai khi Agent crash, nên chưa hứa exactly-once execution.
