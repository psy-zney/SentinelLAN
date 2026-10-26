# Tải và cài SentinelLAN Agent

Chỉ cài trên thiết bị được công ty cho phép. Nhận **mã kết nối** từ IT/Admin; mã chỉ đăng ký được một máy và có thời hạn. Bộ cài nội bộ đã ghim địa chỉ máy chủ công ty; mã không cho đổi máy chủ. Đã dùng thì không thể dùng lại dù còn thời gian. Không chia sẻ mã trong ảnh, ticket hoặc lịch sử dòng lệnh.

## Lấy gói cài

Với bản dùng nội bộ, ưu tiên liên kết bộ cài trên chính máy chủ công ty do IT cấp. Tệp `agent-release.json` đi kèm phải ghi đúng HTTPS công ty và `companyPinned: true`. Không dùng lại EXE cũ chỉ vì tên tệp giống nhau. EXE dịch vụ không phải giao diện cài đặt; bản thiếu cấu hình sẽ báo lỗi, không tự kết nối localhost.

Mở [GitHub Releases](https://github.com/psy-zney/SentinelLAN/releases) và chọn bản do IT chỉ định. Nếu chưa có bản phát hành hoặc thiếu gói phù hợp, liên hệ IT; không tải từ nguồn khác.

Bản phát hành mới chỉ có **`SentinelLAN.Setup.exe` cho Windows x64**. EXE chứa Agent, MSI nội bộ đã sửa và trình cấu hình; không tải Agent.exe hoặc MSI rời từ release cũ. Các gói Linux chỉ còn trong release lịch sử.

## Kiểm tra tệp

Đối chiếu SHA-256 trong phần mô tả của chính release đã tải. Checksum giúp phát hiện tải lỗi/thay đổi tệp, không thay thế xác minh nguồn tải.

```powershell
Get-FileHash .\SentinelLAN.Setup.exe -Algorithm SHA256
```

## Windows

1. Mở `SentinelLAN.Setup.exe`, cho phép cài đặt bằng tài khoản quản trị Windows của IT.
2. Dán **mã kết nối** vào ô duy nhất, bấm **Cài đặt và kết nối**. Không cần nhập địa chỉ máy chủ hoặc nhớ lệnh.
3. Chờ kiểm tra mạng, cài dịch vụ, đăng ký thiết bị và gửi trạng thái đầu tiên. Chỉ khi hoàn tất mới hiện **Kết nối thành công**.

Không dùng MSI 0.2.24: bản đó có thể lỗi Windows Installer 1939. Setup mới chứa bản sửa và tự cài dịch vụ. Có thể mở lại **SentinelLAN - Ket noi thiet bi** từ Start menu. Không đưa mã vào tham số dòng lệnh.

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

Ảnh hỗ trợ do nhân viên chủ động chọn và gửi. Liên hệ IT khi cần cập nhật/cấu hình lại/gỡ Agent. Bản Windows mới hỗ trợ tự cập nhật khi IT đã bật tác vụ và cấu hình nguồn phát hành có chữ ký; bản cũ cần nâng cấp một lần. Xem [hướng dẫn tự cập nhật và gỡ bản cũ](guides/agent-auto-update.md). Không chạy bộ cài khác nguồn lên bản MSI đang hoạt động.
