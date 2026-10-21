# Cảnh báo và khôi phục vận hành

Worker chạy mỗi phút, chỉ kiểm tra thiết bị chưa thu hồi trong công ty đang hoạt động. Application quyết định quy tắc; các port riêng đọc trạng thái thiết bị và quản lý alert. Không tạo lệnh điều khiển máy từ cảnh báo.

| Quy tắc | Điều kiện |
|---|---|
| Offline | Có heartbeat trước đó nhưng không nhận heartbeat mới ít nhất 5 phút; bỏ qua trong thời gian bảo trì được duyệt. |
| DiskHigh | Mẫu mới không quá 2 phút cho thấy ổ hệ thống đã dùng ít nhất 95%. |
| CpuHigh / RamHigh | Các mẫu mới cho thấy mức sử dụng ít nhất 90% trong 5 phút, không có khoảng trống lấy mẫu quá 2 phút. |

Mỗi thiết bị và quy tắc chỉ có một alert tự động đang mở, được bảo vệ thêm bằng unique index PostgreSQL. Cảnh báo tự đóng khi dữ liệu đủ mới cho thấy điều kiện đã bình thường. Thiếu telemetry không được coi là đã khôi phục. Sau khi IT đóng cảnh báo, cùng quy tắc không tạo cảnh báo khác trong một giờ tính từ lần phát trước. Việc sinh và tự đóng đều ghi audit với actor hệ thống (`Guid.Empty`). Thông báo offline trong luồng self-service vẫn giữ chu kỳ riêng.

Telemetry gửi bù được đánh giá theo thời điểm thu thập; mẫu cũ không tạo cảnh báo như mẫu hiện tại. Dữ liệu kỹ thuật quá 30 ngày được dọn theo batch mỗi phút; các API telemetry không trả mẫu ngoài thời gian lưu giữ.

Khi đặt lại mật khẩu qua liên kết kích hoạt, mọi refresh session hiện có bị thu hồi và phiên bản bảo mật đổi. Refresh token mới phải khớp phiên bản bảo mật hiện tại. Refresh session cũ chưa có phiên bản bảo mật bị từ chối sau migration này; người dùng cần đăng nhập lại.

Giới hạn endpoint nhạy cảm dùng route đã khớp thay cho URL người gọi gửi, nên các biến thể chữ hoa/thường và route ID không tạo bucket mới. SignalR lưu hạn phiên đã xác thực và tự đóng khi hết hạn. Sau thay đổi tài khoản/công ty qua API, các kết nối được kiểm tra lại; worker kiểm tra mỗi 5 giây để phát hiện thay đổi ngoài luồng API và đóng kết nối khi không xác minh được quyền.

Hạn nhận biên lai lệnh là 7 ngày sau hạn bắt đầu thực thi. Lệnh đã giao mà chưa có biên lai hiển thị `ExecutionUnconfirmed`; IT cần xác minh trên thiết bị trước khi thực hiện lại. API vẫn từ chối thực thi mới sau hạn và từ chối biên lai cho lệnh chưa từng giao.
