# Quyết định: chỉ giữ Admin, Employee, Agent và mobile

Portal platform cũ đã được chuyển khỏi apps vào docs/private/archive/platform-app-20261009 để giữ bản lưu riêng tư.

Ngày: 2026-10-09. Trạng thái: đã triển khai trong workspace, chưa triển khai lên VPS.

Người dùng chỉ có Admin và Employee. Agent xác thực bằng danh tính thiết bị riêng. Mobile dùng quyền Admin hoặc Employee; không có vai trò Mobile riêng. Admin tiếp nhận công việc quản lý thiết bị và hỗ trợ trước đây của Technician.

Đã gỡ dịch vụ, route, cookie và policy PlatformOwner, đăng nhập platform, quản trị công ty toàn hệ thống và SSH/VPS khỏi ứng dụng. Chế độ triển khai chỉ còn SelfHost và không thể bật lại platform bằng biến môi trường. Bộ cài công ty chứa HTTPS endpoint và public key; mã đăng ký là token không chứa địa chỉ máy chủ. Production yêu cầu chữ ký RSA và bí mật do thiết bị tự tạo.

Migration RetireLegacyRoles khóa tài khoản có vai trò không được hỗ trợ, đổi security stamp, thu hồi refresh và activation token, đồng thời ghi audit. Không tự nâng Technician thành Admin. Giữ dữ liệu và lịch sử migration của hệ thống cũ để phục hồi và điều tra; các bảng platform/VPS cũ không còn dịch vụ ứng dụng truy cập. Tên trường assignedTechnicianId trong hợp đồng được giữ để tương thích; người được phân công phải là Admin đang hoạt động trong cùng công ty.

Nội dung và người viết tin nhắn hỗ trợ, tiêu đề/mô tả yêu cầu, tên và bytes ảnh đính kèm được mã hóa AES-256-GCM bằng khóa dữ liệu 32 bytes độc lập. Mỗi lần ghi dùng nonce ngẫu nhiên 12 bytes và tag xác thực 16 bytes, kèm purpose cho từng trường. EF giải mã khi đọc; thông tin này không được tìm kiếm theo ciphertext. Startup chuyển dữ liệu hỗ trợ cũ sang dạng mã hóa theo lô 100 trước khi nhận request. Khóa phải được giữ cùng bản sao lưu; mất khóa sẽ không đọc được dữ liệu. Đổi khóa phải có quy trình giải mã/mã hóa lại, không chỉ thay biến môi trường.

Email và metadata dùng cho tra cứu, telemetry và audit vẫn cần volume/database và backup được mã hóa ở tầng vận hành. Không tuyên bố rằng toàn bộ PostgreSQL đã được mã hóa chỉ vì có TLS hoặc mã hóa trường hỗ trợ. Mật khẩu, refresh token, activation/enrollment token và device secret được lưu dưới dạng hash. Danh tính và hàng đợi Agent được bảo vệ bởi DPAPI/AES; mobile dùng SecureStore.

Domain chung manager.zney295.id.vn: /company cho Admin, /employee cho Employee và /api/v1 cho mobile/Agent. VPS-Cong dùng Cloudflare DNS proxy, Full (strict), xác thực origin pulls và HTTPS qua Nginx. Không cài Tunnel trên VPS dùng chung. Gateway mới bind 127.0.0.1:9003; 80/443 của Nginx và các ứng dụng khác giữ nguyên. TLS giữa gateway, web/API/PostgreSQL có xác minh CA; PostgreSQL yêu cầu TLS, client certificate và SCRAM.
