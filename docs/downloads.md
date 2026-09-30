# Tải và cài SentinelLAN Agent

Chỉ cài trên thiết bị được tổ chức cho phép. Chuẩn bị địa chỉ HTTPS của hệ thống và mã đăng ký một lần do Admin công ty cấp. Không chia sẻ mã trong ảnh, ticket hoặc lịch sử dòng lệnh.

## Lấy gói cài

Mở [GitHub Releases](https://github.com/psy-zney/SentinelLAN/releases) và chọn bản do IT chỉ định. Nếu chưa có bản phát hành hoặc thiếu gói phù hợp, liên hệ IT; không tải từ nguồn khác.

| Hệ điều hành | Tệp từ cùng bản phát hành |
|---|---|
| Windows x64 | `SentinelLAN.Agent.msi`, `configure-windows-agent.ps1`, `SHA256SUMS.txt` |
| Linux x64 với systemd | `SentinelLAN.Agent`, `install-linux-agent.sh`, `sentinellan-agent.service`, `SHA256SUMS.txt` |

`SentinelLAN.Agent.exe` là binary Windows cho IT triển khai thủ công, không phải trình cài đặt MSI. Điện thoại không cài các gói Agent này.

## Kiểm tra tệp

Đối chiếu SHA-256 với `SHA256SUMS.txt` từ cùng bản phát hành. Checksum giúp phát hiện tải lỗi/thay đổi tệp, không thay thế xác minh nguồn tải.

Windows PowerShell:

```powershell
Get-FileHash .\SentinelLAN.Agent.msi -Algorithm SHA256
Get-FileHash .\configure-windows-agent.ps1 -Algorithm SHA256
```

Linux, trong thư mục vừa tải:

```bash
sha256sum -c SHA256SUMS.txt --ignore-missing
```

## Windows

1. Mở MSI và hoàn tất cài đặt với quyền quản trị.
2. Mở PowerShell bằng **Run as administrator**, chuyển tới thư mục chứa script.
3. Thay địa chỉ ví dụ bằng địa chỉ IT cấp rồi chạy:

```powershell
.\configure-windows-agent.ps1 -ServerUrl 'https://sentinel.example.com'
Get-Service SentinelLANAgent
```

Script hỏi mã đăng ký qua prompt ẩn và cấu hình dịch vụ tự khởi động. Không tắt chính sách bảo vệ máy để chạy script; nếu bị chặn, nhờ IT hỗ trợ.

## Linux

Giữ ba tệp Linux trong cùng một thư mục. Máy cần systemd, OpenSSL và quyền sudo. Thay địa chỉ ví dụ bằng địa chỉ IT cấp:

```bash
sudo bash ./install-linux-agent.sh --server https://sentinel.example.com
systemctl is-active sentinellan-agent
```

Installer hỏi mã qua prompt ẩn, tạo tài khoản dịch vụ và cấu hình tự khởi động. Không sao chép dữ liệu định danh Agent từ máy khác.

## Xác nhận và xử lý lỗi

Nhờ IT kiểm tra máy xuất hiện và có heartbeat mới, sau đó Admin gán máy cho đúng nhân viên. Dịch vụ đang chạy chưa đủ chứng minh đã kết nối. Thao tác cần phiên desktop/quyền bổ sung phải được IT cấu hình riêng.

Nếu đăng ký thất bại, kiểm tra mạng, HTTPS, thời gian máy và hạn mã. Mã đã dùng/hết hạn cần Admin cấp lại; không gửi mã hoặc cấu hình bí mật khi báo lỗi. Dữ liệu hiển thị có thể cũ khi mất mạng; kết nối lỗi không có nghĩa máy đã hỏng.

Ảnh hỗ trợ do nhân viên chủ động chọn và gửi. Liên hệ IT khi cần cập nhật/cấu hình lại/gỡ Agent; không chạy installer khác lên bản MSI đang hoạt động.
