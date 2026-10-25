# ADR 0032: Đăng nhập cho một công ty nội bộ

Ngày: 2026-10-24. Trạng thái: chấp nhận.

SentinelLAN SelfHost phục vụ một công ty do đơn vị tự vận hành. Người dùng không chọn công ty khi đăng nhập. Cả web và mobile gửi email/mật khẩu tới máy chủ đã được cấu hình bởi IT.

Máy chủ lấy phạm vi đăng nhập từ `SENTINELLAN_LOGIN_ORG_CODE`, mặc định là `SENTINELLAN_BOOTSTRAP_ORG_CODE`; Development mặc định `demo`. Ngoài Development, cấu hình công ty phải hợp lệ trước khi API khởi động. Không tìm tài khoản theo email xuyên các công ty. Không tự chọn công ty đầu tiên trong database.

`organizationCode` trong JSON của `POST /api/v1/auth/login` và `POST /api/v1/mobile/auth/login` trở thành nullable/không bắt buộc. Client mới bỏ trường này. Client cũ được dùng khi mã khớp cấu hình; mã khác bị từ chối dù email/mật khẩu có thể hợp lệ ở công ty khác. Các trường phạm vi trong dữ liệu và phản hồi phiên mobile được giữ để bảo vệ phân quyền và khả năng kiểm toán.

Vai trò Admin/Employee vẫn lấy từ tài khoản đã xác thực. Việc truy cập đường dẫn `/company` không tự cấp quyền Admin. Quyền xem tình trạng VPS cũng đối chiếu với đơn vị vận hành trong cấu hình; khi đổi mã công ty cần đồng bộ cấu hình này. Không thay đổi dữ liệu thiết bị hoặc mật khẩu khi điều chỉnh cách đăng nhập.

Kiểm tra gồm đăng nhập web/mobile chỉ với email/mật khẩu, client cũ có mã khớp, từ chối công ty khác, giữ nguyên quyền Admin/Employee, và kiểm tra thực tế quyền xem VPS sau cập nhật cấu hình.

Giao diện quản trị nội bộ không hiển thị mã công ty hoặc ngày khởi tạo đơn vị. Danh sách tài khoản phục vụ việc cấp quyền cho nhân viên và quản trị viên; không phụ thuộc API thông tin công ty. `GET /api/v1/auth/session` bổ sung `userId` của chính người đã xác thực để đánh dấu tài khoản đang dùng và không hiển thị nút tự khóa. Máy chủ vẫn từ chối tự khóa và bảo vệ quản trị viên cuối cùng; giao diện không thay thế kiểm tra quyền này. Tên bootstrap mặc định được trình bày thành “Quản trị hệ thống”, không đổi tên thật hay dữ liệu kiểm toán.
