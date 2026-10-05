# Tải và cài SentinelLAN Agent

Chỉ cài trên thiết bị được tổ chức cho phép. Nhận **mã kết nối** từ IT/Admin công ty; mã chứa sẵn địa chỉ máy chủ, chỉ đăng ký được một máy và có thời hạn. Đã dùng thì không thể dùng lại dù còn thời gian. Không chia sẻ mã trong ảnh, ticket hoặc lịch sử dòng lệnh.

## Lấy gói cài

Mở [GitHub Releases](https://github.com/psy-zney/SentinelLAN/releases) và chọn bản do IT chỉ định. Nếu chưa có bản phát hành hoặc thiếu gói phù hợp, liên hệ IT; không tải từ nguồn khác.

| Hệ điều hành | Tệp từ cùng bản phát hành |
|---|---|
| Windows x64 | `SentinelLAN.Setup.exe` hoặc `SentinelLAN.Agent.msi`, `SHA256SUMS.txt` |
| Linux x64 với systemd | `SentinelLAN.Agent`, `install-linux-agent.sh`, `sentinellan-agent.service`, `SHA256SUMS.txt` |

`SentinelLAN.Setup.exe` là bộ cài có giao diện, chứa MSI và thành phần cấu hình. MSI cũng chứa cửa sổ kết nối và lối tắt trong Start menu. `SentinelLAN.Agent.exe` vẫn là binary dịch vụ cho IT triển khai thủ công. Bản phát hành cũ chưa có Setup.exe vẫn dùng script cấu hình của chính bản đó. Điện thoại không cài các gói Agent này.

## Kiểm tra tệp

Đối chiếu SHA-256 với `SHA256SUMS.txt` từ cùng bản phát hành. Checksum giúp phát hiện tải lỗi/thay đổi tệp, không thay thế xác minh nguồn tải.

Windows PowerShell:

```powershell
Get-FileHash .\SentinelLAN.Agent.msi -Algorithm SHA256
Get-FileHash .\SentinelLAN.Setup.exe -Algorithm SHA256
```

Linux, trong thư mục vừa tải:

```bash
sha256sum -c SHA256SUMS.txt --ignore-missing
```

## Windows

1. Mở `SentinelLAN.Setup.exe`, cho phép cài đặt bằng tài khoản quản trị Windows của IT.
2. Dán **mã kết nối** vào ô duy nhất, bấm **Cài đặt và kết nối**. Không cần nhập địa chỉ máy chủ hoặc nhớ lệnh.
3. Chờ kiểm tra mạng, cài dịch vụ, đăng ký thiết bị và gửi trạng thái đầu tiên. Chỉ khi hoàn tất mới hiện **Kết nối thành công**.

Nếu dùng MSI, hoàn tất bộ cài rồi nhập mã trong cửa sổ kết nối tự mở. Có thể mở lại **SentinelLAN - Ket noi thiet bi** từ Start menu. Không đưa mã vào thuộc tính MSI hay tham số dòng lệnh. Thành phần cấu hình đã nằm trong bộ cài; script riêng chỉ dành cho IT khi cần cấu hình nâng cao.

Máy đã đăng ký không cần mã mới khi bật lại. Bộ cài không tự ghi đè định danh đã có. Khi báo mã đã dùng/hết hạn, nhờ IT cấp mã mới. Nếu mất kết nối sau bước đăng ký, nhờ IT kiểm tra máy đã xuất hiện chưa trước khi thử lại: mã có thể đã được dùng thành công ở máy chủ.

## Linux

Giữ ba tệp Linux trong cùng một thư mục. Máy cần systemd, OpenSSL và quyền sudo. Thay địa chỉ ví dụ bằng địa chỉ IT cấp:

```bash
sudo bash ./install-linux-agent.sh --server https://sentinel.example.com
systemctl is-active sentinellan-agent
```

Installer hỏi mã qua prompt ẩn, tạo tài khoản dịch vụ và cấu hình tự khởi động. Không sao chép dữ liệu định danh Agent từ máy khác.

## Xác nhận và xử lý lỗi

Nhờ IT kiểm tra máy xuất hiện và có heartbeat mới, sau đó Admin gán máy cho đúng nhân viên. Thao tác cần phiên desktop/quyền bổ sung phải được IT cấu hình riêng.

Nếu đăng ký thất bại, kiểm tra mạng, HTTPS, thời gian máy và hạn mã. Mã đã dùng/hết hạn cần Admin cấp lại; không gửi mã hoặc cấu hình bí mật khi báo lỗi. Dữ liệu hiển thị có thể cũ khi mất mạng; kết nối lỗi không có nghĩa máy đã hỏng.

Ảnh hỗ trợ do nhân viên chủ động chọn và gửi. Liên hệ IT khi cần cập nhật/cấu hình lại/gỡ Agent; không chạy installer khác lên bản MSI đang hoạt động.
