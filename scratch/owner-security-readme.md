# Khóa và bản sao lưu SentinelLAN

Đây là thư mục riêng của chủ hệ thống. Không gửi file khóa vào chat, Git hoặc VPS.

Các file trong `backups` đã được mã hóa. Máy này tự tải bản sao lưu từ VPS mỗi ngày 09:45 khi bạn đã đăng nhập. Nếu máy tắt, lần đồng bộ cần chờ máy bật và đăng nhập. Lịch trên VPS vẫn tạo bản sao lưu riêng mỗi ngày.

**Việc bạn cần làm:** mở PowerShell trong thư mục này, chạy `python export-recovery.py`. Mật khẩu được nhập ngay trên máy bạn, không hiện ra màn hình. Chọn mật khẩu dài, riêng biệt. Sau đó chép thư mục `portable-recovery` sang USB hoặc nơi lưu trữ offline và giữ mật khẩu ở nơi khác. Những file DPAPI hiện tại phụ thuộc máy Windows và tài khoản này; mất cả máy lẫn bản xuất khóa có thể làm bản sao lưu không mở được nữa.

Không xóa `backup-private.pem`, `backup-pass.txt.dpapi`, `ca.key.dpapi` hoặc các file `.crt` trước khi có bản khóa khôi phục được giữ an toàn. Khóa trên máy hiện tại đã được giới hạn quyền truy cập và bảo vệ bằng mật khẩu/Windows DPAPI.

Kiểm tra khôi phục bản sao lưu: `python verify-backup.py`. Kiểm tra này dùng một database tạm riêng trên VPS, không sửa database đang chạy. Đã kiểm tra thành công đủ 36 bảng ở thời điểm triển khai.

VPN/MFA và mã hóa ổ đĩa database chưa được bật. Trang đăng nhập quản trị vẫn mở từ Internet. Cấu hình hiện tại chỉ dùng TLS 1.3; Agent dùng kết nối chuẩn của Windows cần Windows 11 hoặc Server 2022 trở lên. Khóa TLS nội bộ cần được gia hạn trước tháng 10/2027; VPS đã có lịch kiểm tra hạn chứng chỉ.

Chi tiết kỹ thuật nằm ở `C:\Users\admin\MyProject\SentinelLAN\docs\private\security-hardening-2026-10-03.md` và `/opt/sentinellan/shared/security-hardening-2026-10-03.md` trên VPS.
