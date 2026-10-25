# Đăng ký Agent được ủy quyền

Admin tạo token enrollment 1–60 phút trong dashboard, ghi lý do và xác nhận. Token chỉ hiển thị một lần; server lưu hash và từ chối tái sử dụng. Cài Agent trên thiết bị thuộc quyền quản lý của tổ chức, dùng DNS/chứng chỉ HTTPS tin cậy. Không truyền token trong URL, log, issue hoặc kênh chat công khai.

## Mã kết nối và máy chủ

API cần `SENTINELLAN_AGENT_PUBLIC_URL` là địa chỉ gốc HTTPS truy cập được từ thiết bị công ty (ví dụ `https://sentinel.company.local`). Cấu hình Production Compose mặc định lấy `https://PUBLIC_DOMAIN`; LAN có thể ghi đè bằng tên DNS nội bộ. Không lấy URL từ Host/Forwarded header của người gọi. Development mặc định loopback HTTP để thử trên cùng máy; không cấp mã loopback cho máy nhân viên trong LAN.

SelfHost trả `connectionCode` là token một lần, không chứa địa chỉ máy chủ. Bộ cài công ty ghim sẵn HTTPS và khóa công khai xác minh lệnh. Dạng `SL1.<base64url-json>` chỉ thuộc bộ cài tổng quát cũ; không dán SL1 vào bộ cài công ty. Không đưa khóa riêng, credential thiết bị hoặc chứng chỉ vào mã. Agent kiểm tra HTTPS/chứng chỉ hệ thống; máy chủ xác nhận hạn token và tiêu thụ trong giao dịch tạo thiết bị.

`SentinelLAN.Agent.exe` là chương trình dịch vụ; `SentinelLAN.Setup.exe` mới là bộ cài có giao diện. Bản công ty cần build cả hai với `CompanyServerUrl`, `CommandPublicKey` và `CommandKeyId` tương ứng VPS. Khi thiếu cấu hình, Agent báo lỗi thay vì tự dùng localhost. Bản Production từ chối loopback và địa chỉ khác công ty đã ghim; Development chỉ cho phép localhost khi cấu hình rõ ràng.

Trước khi cài, IT chạy `SentinelLAN.Agent.exe --check-configuration` và `SentinelLAN.Setup.exe --check-configuration` qua tiến trình có thu stdout để đối chiếu `ServerUrl`, `CompanyPinned`, `CommandKeyId`. Chế độ này không đăng ký máy, không gửi telemetry và không thay dịch vụ. `agent-release.json` và `SHA256SUMS.txt` đi kèm ghi cấu hình công khai và checksum. Bộ cài hiển thị địa chỉ công ty ngay trên cửa sổ kết nối. Sau cài, cấu hình vận hành nằm ở `%ProgramData%\SentinelLAN\Agent\agent-settings.json`; file này có quyền hạn chế và không được chia sẻ toàn bộ.

## Windows

Người cài ưu tiên `SentinelLAN.Setup.exe`: nhập một mã kết nối và bấm **Cài đặt và kết nối**. EXE chứa MSI và script cấu hình; MSI chứa configurator, tự mở cửa sổ sau khi cài bằng giao diện và có shortcut Start menu để mở lại. Cài silent MSI vẫn để cấu hình cho IT. Không truyền token qua CLI/MSI property: configurator chuyển token qua stdin của tiến trình cấu hình. Dữ liệu tạm nằm trong ProgramData có ACL hạn chế và token được xóa khi hoàn tất/lỗi.

Cửa sổ chỉ báo thành công sau heartbeat đầu tiên. `enrollment-status.json` chỉ lưu trạng thái và thời điểm, không có token/secret. Lỗi `TokenUsed`/`TokenExpired` được hiển thị riêng. Máy đã có identity không bị configurator ghi đè hoặc tiêu thụ mã khác; thu hồi và đăng ký lại là thao tác IT riêng. Không bật thêm quyền lab hoặc phân phối signing key trong mã kết nối.

Từ bản `0.2.26`, mở setup trên máy đã có identity sẽ tự bỏ qua ô mã, dùng địa chỉ máy chủ trong cấu hình hiện có và chuyển sang kiểm tra/khôi phục dịch vụ. Bộ cài công ty từ chối cấu hình của máy chủ khác. Setup khởi động lại dịch vụ và chờ heartbeat mới; không suy thành công chỉ từ identity hay biên nhận cũ. Thành công có nút **Hoàn tất**; lỗi có **Thử lại kết nối** và **Đóng**. Nếu lần đăng ký trước đã lưu identity nhưng chưa xác nhận heartbeat, lần thử tiếp theo tự tiếp tục bằng identity đó, không dùng lại token. Nếu identity mất trước bước tiếp tục, setup trở lại bước nhập mã. Đóng trong lúc cài/cấu hình sẽ chờ bước đang chạy kết thúc để dọn token và file tạm an toàn.

Nếu bộ cài báo lỗi Windows Installer `1603`, kiểm tra Application Event Log, nguồn `MsiInstaller`, để lấy lỗi cụ thể. Bản `0.2.24` có thể gặp lỗi `1939` khi MSI cấu hình phục hồi dịch vụ; dùng bộ cài `0.2.25` đã sửa. MSI để dịch vụ ở trạng thái chờ; script cấu hình đặt phục hồi bằng `sc.exe` trước khi khởi động, tránh [lỗi đã được Microsoft ghi nhận trong MsiConfigureServices](https://learn.microsoft.com/en-us/windows/win32/msi/msiconfigureservices-action). Lỗi ở bước MSI xảy ra trước khi gửi mã đăng ký; nếu mã đã hết hạn khi thử lại, Admin cần cấp mã mới.

### Cấu hình nâng cao bằng script (IT)

Windows MSI là lựa chọn chính. Tải `SentinelLAN.Agent.msi`, `configure-windows-agent.ps1` và `SHA256SUMS.txt` từ cùng một GitHub Release; kiểm tra SHA-256 trước khi cài. Mở PowerShell Administrator:

```powershell
msiexec /i .\SentinelLAN.Agent.msi
.\configure-windows-agent.ps1 -ServerUrl 'https://sentinel.example.com'
```

MSI cài binary, cửa sổ kết nối và đăng ký service LocalService ở trạng thái chờ. Luồng script bên dưới dành cho IT; nhân viên dùng cửa sổ kết nối đã đi kèm. Script hỏi token qua prompt, lưu tạm trong thư mục ProgramData chỉ dành cho SYSTEM, Administrators và tài khoản service, chạy service và xóa token khỏi file cấu hình sau khi đăng ký hoặc khi xảy ra lỗi. Không đưa token vào tham số `msiexec`, log, issue hay trang tải. Kiểm tra `Get-Service SentinelLANAgent` và heartbeat sau khi cấu hình. Không gỡ MSI khi chỉ cần đổi URL; IT có thể chạy lại script với URL mới.

EXE là lựa chọn phụ để triển khai thủ công hoặc kiểm tra. Quy trình dưới đây dành riêng cho EXE và không dùng sau khi đã cài MSI vì sẽ đăng ký service chồng lên bộ cài. Nếu máy đã có service cài bằng script EXE cũ, hãy gỡ service cũ theo quy trình vận hành, thu hồi định danh thiết bị cũ rồi cài MSI và đăng ký lại bằng token mới; không sao chép file identity giữa tài khoản dịch vụ.

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

Script hỏi token, tạo user dịch vụ `sentinellan`, khóa identity riêng trong `/etc/sentinellan/agent.env` và dữ liệu trong `/var/lib/sentinellan-agent`. Thư mục dữ liệu Linux phải chỉ chủ sở hữu truy cập (mode 0700); Agent từ chối ghi vào thư mục có quyền nhóm/người khác thay vì tự đổi quyền thư mục dùng chung. Khóa Linux phải được giữ khi cài lại; mất khóa thì cần thu hồi device cũ rồi đăng ký lại. Có thể truyền `--signing-key-file <root-only-file>` nếu vận hành luồng lệnh có chữ ký; khóa phải khớp server và phải phân phối bằng kênh riêng. Không bật lệnh khóa/cô lập thật; mã hiện chỉ mô phỏng.

## Kiểm tra và xử lý lỗi

Windows Service cần desktop companion để thực thi thông báo/khóa/idle policy trên console. Xem [hướng dẫn thực thi thật](real-agent-execution.md) để đăng ký companion, cấu hình quyền và nghiệm thu; `SimulateLock` không khóa Windows. Linux hiện chỉ hỗ trợ telemetry thật, lưu trữ bảo vệ và xác thực/biên lai; các adapter OS mới trong ADR 0007 yêu cầu Windows.

1. Từ thiết bị Agent, thử `https://<dns>/health/live` và xác nhận chứng chỉ hợp lệ. `health/ready` 503 là lỗi kết nối DB phía server.
2. Kiểm tra service (`Get-Service SentinelLANAgent` trên Windows; `systemctl status sentinellan-agent` trên Linux), rồi xem thiết bị trong inventory và thời điểm heartbeat. Online có thể cần một chu kỳ gửi tiếp theo.
3. Nếu enroll trả 400, token có thể sai, hết hạn hoặc đã dùng. Admin tạo token mới; không sửa DB để tái dùng token cũ.
4. Nếu Agent bị thu hồi, header credential cũ trả 401. Cài lại bằng token mới sau khi xác minh thiết bị; không sao chép secret từ thiết bị khác.

Khi offline, Agent giữ tối đa 50 mục telemetry trong một giờ và retry với idempotency key cũ. Production lưu queue trong file được bảo vệ (DPAPI trên Windows; AES-GCM với `SENTINELLAN_AGENT_STORE_KEY` trên Linux) và nạp lại sau restart. Development mặc định dùng RAM nên mất queue khi tiến trình khởi động lại. Cần kiểm tra quyền file và khôi phục trên host thật; queue không phải bản sao lưu.
