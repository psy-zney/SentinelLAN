# Thực thi Agent thật và kiểm chứng môi trường

Agent phải được đăng ký bằng token hợp lệ, có heartbeat thật và có khóa kiểm tra lệnh khớp API. Production dùng HTTPS với chứng chỉ tin cậy, PostgreSQL, credential/nonce/queue/receipt được bảo vệ. `scripts/test-production-readiness.ps1 -ServerUrl https://<api-dns> -CheckWindowsAgent` kiểm tra kết nối thật, báo `BLOCKED` khi thiếu thành phần và trả exit code 1. Script không bật tính năng hay sửa hệ điều hành. Kiểm tra machine environment chỉ là một nguồn cấu hình; cấu hình riêng trong `agent-settings.json` cần Admin kiểm tra tại host.

| Lệnh | Hành động và bằng chứng biên lai |
|---|---|
| `CollectTelemetryNow` | Đọc CPU/RAM/disk mới, gửi heartbeat với key riêng của command; chỉ thành công khi HTTP được xác nhận. |
| `ShowNotification` | Gọi Windows WTS API trên phiên người dùng; báo mã phản hồi/timeout, không khẳng định người dùng đã đọc. |
| `RestartService` | Stop rồi Start đúng service `docker`, `nginx` hoặc `caddy`; kiểm tra lại Stopped/Running. Tên service phải tồn tại thực tế. |
| `RefreshPolicy` | GET policy của thiết bị đã xác thực; áp idle/screen saver người dùng và USB registry; đọc lại cấu hình. USB đang gắn có thể cần tháo/gắn lại hoặc khởi động lại. |
| `LockWorkstation` | Gọi `LockWorkStation`, quan sát trạng thái khóa qua WTS; Service kiểm tra lại phiên console. |
| `IsolateNetwork` | Firewall chặn IPv4/IPv6 hai chiều, chừa toàn bộ IP API cấu hình; xác minh rule ActiveStore. Recovery task SYSTEM được đăng ký trước, chạy sau 30–60 giây hoặc sau reboot. Phải kiểm tra lưu lượng thật. |
| `SimulateLock`, `SimulateNetworkIsolation` | Chỉ mô phỏng, có nhãn riêng trong lịch sử; không tính vào số hành động thật thành công. |

## Windows Service và desktop companion

Service trong Session 0 không có desktop người dùng. Cài MSI/EXE hiện có, đăng ký Agent, rồi đăng ký companion bằng PowerShell Administrator:

```powershell
.\deploy\agent\install-desktop-companion.ps1 -UserName 'DOMAIN\authorized-user'
```

Task dùng interactive token của user, không lưu mật khẩu. Binary phải nằm trong Program Files có ACL được bảo vệ. Companion không đọc credential hay khóa ký của Agent. Chỉ phiên console đang hoạt động được chọn; RDP-only cần adapter riêng và sẽ trả lỗi. Không nâng toàn bộ Production Service lên LocalSystem để né vấn đề Session 0.

Để thử EXE trong phiên user, có thể chạy binary Agent trực tiếp với cấu hình tương ứng; adapter desktop hoạt động trong phiên đó. Không chạy thêm Agent dùng cùng identity khi Service vẫn đang chạy.

## Cấu hình quyền tại thiết bị được phép

Các biến sau là cấu hình cục bộ; không thể bật qua lệnh từ dashboard:

```text
SENTINELLAN_LAB_EXECUTION=true
SENTINELLAN_LAB_DEVICE_ID=<GUID thiết bị lab đã đăng ký>
SENTINELLAN_ALLOW_SERVICE_RESTART=true
SENTINELLAN_ALLOW_POLICY_CHANGES=true
SENTINELLAN_ISOLATION_SERVER_IPV4=<IPv4 thật của API, không phải loopback>
```

Chỉ bật quyền cần kiểm thử. Cấu hình lab/device ID và quyền policy phải có ở cả Service và companion; machine environment là một cách phân phối các cờ không bí mật. Restart các tiến trình sau thay đổi cấu hình. Khóa ký và token phải phân phối qua kênh bảo mật, không đưa vào dòng lệnh/log/Git. Service restart cần ACL kiểm soát đúng service; policy cần quyền registry; isolation cần Agent lab elevated, Windows Firewall bật mọi profile và quyền Task Scheduler. LocalService mặc định không có những quyền này, nên lỗi quyền là trạng thái `Failed`, không phải thành công giả.

## Nghiệm thu bằng thiết bị thật

1. Kiểm tra HTTPS `/health/live`, `/health/ready`, database PostgreSQL và heartbeat của đúng device ID. Đối chiếu CPU/RAM/disk với công cụ Windows trong cùng khoảng lấy mẫu; CPU là tỷ lệ theo delta, không phải số cố định.
2. Gửi `CollectTelemetryNow`; xem mẫu telemetry mới và receipt. Gửi thông báo; quan sát cửa sổ trên console, ghi rõ WTS timeout không phải xác nhận người dùng đã đọc.
3. Trên máy lab được phép, gửi `LockWorkstation` với reason/confirmation. Quan sát màn hình khóa và receipt WTS; đăng nhập lại bằng tài khoản của người dùng. Không dùng `SimulateLock` để chứng minh khóa thật.
4. Restart một service thực sự có trong allow-list; kiểm tra trạng thái trước/sau và audit. Nếu service không tồn tại hoặc thiếu ACL, receipt phải Failed.
5. Gán policy, gửi RefreshPolicy, kiểm tra screen saver và registry. Với USB, thử ổ lưu trữ lab sau reconnect/reboot; không lấy tên policy hiển thị làm bằng chứng thiết bị đã bị chặn.
6. Isolation chỉ thử khi có console cục bộ để khôi phục. Gửi `IsolateNetwork` với parameter `30`–`60`; quan sát bốn rule, thử traffic IPv4/IPv6 tới host khác và giữ kết nối API. Dừng Agent để xác minh recovery độc lập vẫn xóa rule đúng hạn; thử reboot để kiểm tra startup recovery. Ngoại lệ là toàn bộ host IPv4 của API, không chỉ cổng HTTPS. Scheduler bị tắt/hỏng có thể làm recovery trễ; không triển khai ngoài lab khi chưa có bằng chứng này.
7. Thử sai chữ ký, hết hạn, sai device ID, phát lại nonce, thiếu cờ lab và thiếu companion; OS không được tác động, receipt phải báo lỗi. Restart Agent khi gửi receipt thất bại; xác nhận không thực thi lặp.

## Điều kiện còn thiếu trong phiên phát triển ngày 2026-09-26

Máy phát triển có .NET SDK 10.0.400 và Node.js 24.18.0. Sau khi người dùng khởi chạy Docker, Docker Engine 29.8.0 và Compose v5.5.1 đã kết nối được; PostgreSQL 18.1, API HTTP cổng 8080 và web cổng 3000 đang chạy. Các bài thử PostgreSQL và Docker dùng database/project riêng, không ghi đè database của stack đang chạy. Không tìm thấy Service SentinelLANAgent hay desktop companion đang chạy; tiến trình hiện tại không elevated. Chưa có URL HTTPS production với chứng chỉ tin cậy hoặc device ID lab được cấu hình. Vì vậy test tự động và đọc telemetry Windows không chứng minh một triển khai production hoặc một lần khóa/cô lập máy thật. Xem [kết quả kiểm tra đầy đủ](full-verification-2026-09-26.md).

Khóa ký HMAC dùng chung, rotation/public-key pinning và audit bất biến ở mức database vẫn là các điều kiện bảo mật production cần giải quyết; xem ADR 0007. Không gọi hệ thống là production-ready chỉ vì build hoặc InMemory integration tests pass.

## Bằng chứng kết nối cục bộ

Sau `dotnet build SentinelLAN.slnx`, chạy `powershell.exe -NoProfile -File scripts/test-agent-live.ps1`. Bài kiểm tra khởi chạy API và Agent thành hai tiến trình riêng, sử dụng HTTP loopback, enrollment và lệnh `CollectTelemetryNow` có chữ ký, rồi kiểm tra mẫu telemetry Windows và receipt qua API. Mật khẩu/khóa dùng một lần không in ra; identity thử nghiệm được xóa và tiến trình được dừng sau bài thử. Mặc định persistence dùng Development InMemory và được ghi rõ. Để thử PostgreSQL thật, tạo một database mới có tên `sentinellan_agent_live_<hex ngẫu nhiên>`, đặt kết nối trong `ConnectionStrings__SentinelLAN` rồi thêm `-UsePostgres`; script từ chối tên database ứng dụng khác. Bài thử không chứng nhận TLS hay khóa/cô lập desktop.

Các test `WindowsActionPlanTests` thay thế cmdlet OS để kiểm tra recovery phải đăng ký trước firewall, bao phủ IPv4/IPv6, giữ ngoại lệ API và không xóa recovery task khi xóa rule thất bại. Đây là kiểm tra logic điều phối; không thay thế nghiệm thu lưu lượng và Task Scheduler trên máy lab.

Kết quả kiểm tra trong phiên này: 170 test .NET dùng PostgreSQL thật, 25 test web, 48 test mobile và 6 E2E web pass; lint/typecheck web/mobile, build web và Expo export Android/iOS pass. Publish EXE win-x64 self-contained đã pass. Kiểm tra NuGet không báo package có advisory trong dependency tree hiện tại. `npm audit --omit=dev --audit-level=high` trả exit code 0 nhưng vẫn báo 13 cảnh báo mức moderate trong cây phụ thuộc Expo/mobile, thuộc hai advisory đã theo dõi ở [risk register](../security/dependency-risk-register.md). Các cảnh báo này chưa được giải quyết bởi thay đổi Agent. Format toàn solution còn lỗi encoding/newline/namespace ở migration lịch sử; kiểm tra format khi loại riêng thư mục migration pass.
