# Tự cập nhật và gỡ SentinelLAN Agent trên Windows

Tự cập nhật hỗ trợ **Windows x64 đã cài bằng MSI và đã đăng ký thiết bị**. Agent vẫn chạy bằng LocalService; tác vụ `SentinelLAN Agent Update` riêng chạy bằng SYSTEM để nâng cấp MSI. IT phải bật tác vụ một lần trên máy được phép. Linux và ứng dụng mobile chưa dùng cơ chế này.

## Nâng bản cũ lần đầu

Không cần gỡ Agent nếu mục tiêu là nâng cấp và giữ kết nối máy:

1. IT lấy MSI mới từ nguồn tin cậy, kiểm tra SHA-256 và chữ ký nhà phát hành.
2. Chạy `SentinelLAN.Setup.exe` mới với quyền quản trị; với máy đã đăng ký, chọn tiếp tục kết nối máy hiện có. Không xóa `%ProgramData%\SentinelLAN\Agent`, không nhập lại mã đã dùng.
3. Kiểm tra dịch vụ `SentinelLANAgent` hoạt động và dashboard nhận heartbeat mới, đúng phiên bản Agent. Bản cài mới cung cấp script bật tự cập nhật bên dưới.

MSI dùng cùng UpgradeCode để nâng bản tại chỗ, chặn hạ phiên bản và đặt gỡ bản trước trong giao dịch cài đặt để Windows Installer có thể rollback nếu nâng cấp lỗi. Rollback ở đây là khả năng của giao dịch MSI; chưa phải kiểm chứng Agent kết nối thành công sau mỗi nâng cấp.

## IT bật tự cập nhật một lần

Chuẩn bị URL manifest HTTPS do công ty quản lý, khóa **công khai** RSA của hệ thống phát hành và thumbprint chứng chỉ Authenticode được IT tin cậy. Khóa ký bản phát hành tách biệt với khóa ký lệnh Agent. Không đưa khóa riêng/chứng chỉ có khóa riêng lên máy nhân viên.

Trong PowerShell Administrator, thay các giá trị ví dụ bằng cấu hình thật:

```powershell
& "$env:ProgramFiles\SentinelLAN\Agent\configure-agent-updates.ps1" `
  -ManifestUrl 'https://updates.example.com/agent/stable.json' `
  -PublicKeyPath 'C:\IT\agent-release-public.pem' `
  -PublisherThumbprint 'THUMBPRINT_40_KY_TU_HEX_CUA_CHUNG_CHI' `
  -CompanyServerUrl 'https://manager.example.com' `
  -CheckIntervalMinutes 60
```

Script tạo cấu hình trong `%ProgramData%\SentinelLAN\Updates`, chỉ SYSTEM/Administrators được sửa; LocalService không có quyền ghi vào bộ cập nhật. Kiểm tra chạy lúc khởi động và mặc định mỗi 60 phút. IT chọn lịch phù hợp vì việc nâng cấp tạm ngắt telemetry và có thể ảnh hưởng chức năng quản lý trong lúc thay dịch vụ. Cơ chế này chưa gửi thông báo bản mới cho nhân viên.

Có thể yêu cầu kiểm tra ngay:

```powershell
Start-ScheduledTask -TaskName 'SentinelLAN Agent Update'
Get-Content "$env:ProgramData\SentinelLAN\Updates\update-status.json"
```

Tác vụ sao chép binary đã cài vào thư mục chạy được bảo vệ trước khi nâng cấp, để MSI thay được binary trong Program Files. Bộ cập nhật chỉ chấp nhận manifest có chữ ký RSA-PSS SHA-256 với khóa tối thiểu 3072 bit, còn hạn tối đa 7 ngày, đúng công ty và có mã phát hành riêng. MSI phải nằm trên cùng HTTPS origin với manifest, không có redirect, đúng kích thước tối đa 256 MiB, đúng SHA-256, chữ ký Authenticode hợp lệ, thumbprint đã ghim, tên sản phẩm, nhà sản xuất, UpgradeCode, kiến trúc x64 và phiên bản đã ký.

Không tải/cài bản bằng hoặc thấp hơn phiên bản hiện có. Không nhận lệnh thực thi tùy ý. Không tắt xác minh TLS, không ép khởi động lại Windows và không xóa định danh/cấu hình máy.

## Phát hành bản để Agent tự lấy

Workflow GitHub Release thông thường chưa tự ký MSI hay xuất manifest cho công ty. IT thực hiện trong môi trường phát hành được bảo vệ:

1. Build MSI với phiên bản `X.Y.Z` cao hơn bản đã cài, cùng cấu hình máy chủ và khóa kiểm tra lệnh của công ty. Ví dụ dùng `deploy/agent/build-msi.ps1 -Version X.Y.Z` cùng các tham số ghim công ty trong hướng dẫn triển khai hiện có.
2. Ký Authenticode và timestamp MSI bằng chứng chỉ được máy nhân viên tin cậy. Không sửa MSI sau khi ký. Lấy thumbprint từ chứng chỉ ký đã được kiểm tra.
3. Cấp khóa RSA riêng qua biến môi trường `SENTINELLAN_UPDATE_PRIVATE_KEY_PEM` từ kho bí mật của môi trường phát hành; dùng PowerShell 7 chạy:

```powershell
./deploy/agent/new-agent-update-manifest.ps1 `
  -MsiPath './dist/windows-x64/SentinelLAN.Agent.msi' `
  -PackageUrl 'https://updates.example.com/agent/agent-X.Y.Z.msi' `
  -Version 'X.Y.Z' -CompanyServerUrl 'https://manager.example.com' `
  -Reason 'Sửa nhận diện Windows và cải thiện Agent' `
  -OutputPath './dist/windows-x64/stable.json' -ValidHours 72
```

4. Đưa MSI lên URL bất biến, sau đó mới thay manifest `stable.json` trên cùng HTTPS origin. Thử trên máy lab trước khi đổi manifest cho cả nhóm máy.

Manifest tự hết hạn sau thời gian đã ký. Nếu muốn tiếp tục cho phép máy offline lâu cập nhật, IT phát hành lại manifest có hạn mới và mã phát hành mới; không đổi nội dung MSI ở URL cũ. Việc đổi khóa công khai hoặc thumbprint đã ghim cần IT cấu hình lại tại máy; không lấy khóa tin cậy mới từ chính manifest.

## Kiểm tra kết quả và lỗi

`update-status.json` ghi kết quả gần nhất; `update-audit.jsonl` được chương trình ghi nối tiếp với thời gian UTC, mã phát hành, lý do, phiên bản và mã thoát MSI. Tệp chỉ dành cho IT, không gửi khóa hay credential. IT quản lý lưu trữ nhật ký theo chính sách công ty.

| Trạng thái | Ý nghĩa |
|---|---|
| `UpToDate` | Phiên bản đã cài bằng hoặc mới hơn bản trong manifest. |
| `Installing` | Đã lưu mã lần cài trước khi gọi MSI; chưa xác nhận hoàn tất. |
| `Installed` | MSI trả 0, đăng ký phiên bản khớp và dịch vụ đã chạy. Phải kiểm tra heartbeat để xác nhận kết nối máy chủ. |
| `RestartRequired` | MSI trả 3010; không ép reboot. IT sắp lịch khởi động lại rồi kiểm tra heartbeat/phiên bản. |
| `InstallFailed` / `Failed` | Có lỗi kiểm tra/tải/cài hoặc chưa xác nhận phiên bản/dịch vụ. Xem nhật ký Windows Installer và audit. |
| `AlreadyAttempted` | Mã phát hành này từng bắt đầu cài; không tự lặp lại để tránh vòng lặp nâng cấp lỗi. Kiểm tra audit, dịch vụ và bản đã cài trước. |

Mỗi mã phát hành chỉ bắt đầu cài một lần. Nếu MSI lỗi/bận, máy mất điện hoặc tác vụ bị dừng trong lúc cài, IT kiểm tra trước khi phát hành manifest có mã mới để thử lại. Nếu lỗi trước khi bắt đầu MSI, lần kiểm tra kế tiếp có thể tải lại. Không dừng tác vụ giữa giao dịch MSI. Bộ cập nhật không tự hạ phiên bản khi Agent không kết nối; chưa có kiểm thử chấp nhận nâng cấp/gỡ thực trên mọi cấu hình Windows.

## Gỡ Agent cũ hoàn toàn

Chỉ thực hiện khi IT muốn ngừng quản lý hoặc cài lại từ đầu; nâng cấp thông thường không cần gỡ.

1. Nếu có tự cập nhật, tắt bằng PowerShell Administrator **trước khi gỡ**:

   ```powershell
   & "$env:ProgramFiles\SentinelLAN\Agent\configure-agent-updates.ps1" -Disable
   ```

   Script từ chối khi tác vụ đang chạy. Với bản cũ chưa có script/tác vụ này, bỏ qua bước đó.
2. Mở **Cài đặt → Ứng dụng → Ứng dụng đã cài đặt → SentinelLAN Endpoint Agent → Gỡ cài đặt**, đồng ý quyền quản trị. Không chỉ xóa thư mục Program Files và không dùng `sc delete` thay cho gỡ MSI.
3. Nếu IT đã cài desktop companion, kiểm tra trong Task Scheduler và dừng/gỡ các tác vụ `SentinelLAN-Desktop-<SID người dùng>` thuộc đúng máy này. Không xóa các tác vụ khác. Với Agent cũ cài thủ công không xuất hiện trong danh sách ứng dụng, IT cần xác định cách cài ban đầu để gỡ; hướng dẫn MSI này không áp dụng trực tiếp.
4. Kiểm tra không còn dịch vụ `SentinelLANAgent` và không còn mục ứng dụng đã cài. Trên dashboard, máy chuyển offline sau thời gian giám sát; gỡ phần mềm không tự xóa bản ghi thiết bị trên máy chủ. IT thu hồi quyền/định danh máy theo quy trình quản lý thiết bị.

MSI không xóa `%ProgramData%\SentinelLAN\Agent` và thư mục `Updates`; chúng có thể chứa định danh bí mật và nhật ký. Giữ nguyên nếu cần cài lại để nối tiếp thiết bị cũ. Nếu ngừng sử dụng máy, IT xử lý dữ liệu còn lại và credential phía máy chủ theo chính sách, không sao chép định danh sang máy khác. Cài lại sau khi đã xóa định danh cần mã kết nối mới; không dùng lại mã cũ.
