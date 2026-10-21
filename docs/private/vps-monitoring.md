# Kiểm tra VPS trong Super Admin

Mở /platform, đăng nhập tài khoản chủ hệ thống, chọn **Máy chủ**. Phần **VPS đang chạy SentinelLAN** tự hiển thị máy đang phục vụ ứng dụng; không cần nhập lại SSH key.

Trang hiển thị CPU, RAM, ổ đĩa, thời gian chạy, các Docker đang chạy hoặc đã dừng, trạng thái kiểm tra sức khỏe, cổng đang nghe, cổng công bố trên VPS, tài nguyên và chính sách tự khởi động. Có tìm theo tên hoặc image và lọc theo trạng thái. **Kiểm tra VPS** đọc bản đo gần nhất; bộ thu thập trên máy chủ tạo bản mới mỗi phút. Luôn xem thời gian kiểm tra. Dữ liệu quá ba phút được báo cũ.

Cổng hiển thị dạng địa chỉ VPS:cổng → cổng container. Địa chỉ 127.x.x.x hoặc ::1 chỉ lắng nghe trên VPS. Cổng khai báo trong Docker được tách riêng vì ứng dụng có thể dùng cổng khác. Container dùng chung mạng hiển thị cổng của mạng chung. Tường lửa còn quyết định kết nối từ Internet.

Triển khai sử dụng đủ ba Compose file qua deploy/vps/compose-secure.sh. Tạo /opt/sentinellan/shared/host-monitor với root:1654, quyền 0750. Cấu hình tên và địa chỉ máy chủ trong /opt/sentinellan/shared/host-monitor.conf (root:root, 0600) bằng SENTINELLAN_HOST_NAME và SENTINELLAN_HOST_ADDRESS. Cài hai unit sentinellan-host-monitor.service và .timer, chạy daemon-reload, enable --now timer và start service. JSON trạng thái root:1654, 0640; API chỉ đọc thư mục này.

Khi chưa có dữ liệu: kiểm tra systemctl status sentinellan-host-monitor.service, systemctl list-timers sentinellan-host-monitor.timer và quyền đọc bản trạng thái. Không cấp API quyền root hay gắn Docker socket để xử lý lỗi. Không đưa môi trường Docker, mật khẩu hay log ứng dụng vào bản trạng thái.
