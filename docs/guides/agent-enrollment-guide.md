# Đăng ký Agent được ủy quyền

Admin tạo token enrollment 1–60 phút trong dashboard, ghi lý do và xác nhận. Token chỉ hiển thị một lần; server lưu hash và từ chối tái sử dụng. Cài Agent trên thiết bị thuộc quyền quản lý của tổ chức, dùng DNS/chứng chỉ HTTPS tin cậy. Không truyền token trong URL, log, issue hoặc kênh chat công khai.

## Windows

```powershell
dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
```

Chuyển `SentinelLAN.Agent.exe` và `deploy/agent/install-windows-agent.ps1` qua kênh nội bộ được phép. Mở PowerShell Administrator tại nơi chứa hai file:

```powershell
./install-windows-agent.ps1 -ServerUrl 'https://sentinel.example.com'
```

Installer hỏi token qua prompt, đăng ký Windows Service và xóa biến token toàn máy sau khi thấy identity đã được ghi trong `%ProgramData%\SentinelLAN\Agent`. Nếu chưa ghi được, xem trạng thái service và cấp token mới khi token cũ hết hạn. Agent dùng DPAPI để bảo vệ identity dưới tài khoản dịch vụ; không di chuyển file identity sang máy khác.

## Linux

```bash
dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
sudo bash deploy/agent/install-linux-agent.sh --server https://sentinel.example.com
```

Script hỏi token, tạo user dịch vụ `sentinellan`, khóa identity riêng trong `/etc/sentinellan/agent.env` và dữ liệu trong `/var/lib/sentinellan-agent`. Khóa Linux phải được giữ khi cài lại; mất khóa thì cần thu hồi device cũ rồi đăng ký lại. Có thể truyền `--signing-key-file <root-only-file>` nếu vận hành luồng lệnh có chữ ký; khóa phải khớp server và phải phân phối bằng kênh riêng. Không bật lệnh khóa/cô lập thật; mã hiện chỉ mô phỏng.

## Kiểm tra và xử lý lỗi

1. Từ thiết bị Agent, thử `https://<dns>/health/live` và xác nhận chứng chỉ hợp lệ. `health/ready` 503 là lỗi kết nối DB phía server.
2. Kiểm tra service (`Get-Service SentinelLANAgent` trên Windows; `systemctl status sentinellan-agent` trên Linux), rồi xem thiết bị trong inventory và thời điểm heartbeat. Online có thể cần một chu kỳ gửi tiếp theo.
3. Nếu enroll trả 400, token có thể sai, hết hạn hoặc đã dùng. Admin tạo token mới; không sửa DB để tái dùng token cũ.
4. Nếu Agent bị thu hồi, header credential cũ trả 401. Cài lại bằng token mới sau khi xác minh thiết bị; không sao chép secret từ thiết bị khác.

Khi offline, Agent giữ tối đa 50 mục telemetry trong một giờ và retry với idempotency key cũ. Production lưu queue trong file được bảo vệ (DPAPI trên Windows; AES-GCM với `SENTINELLAN_AGENT_STORE_KEY` trên Linux) và nạp lại sau restart. Development mặc định dùng RAM nên mất queue khi tiến trình khởi động lại. Cần kiểm tra quyền file và khôi phục trên host thật; queue không phải bản sao lưu.
