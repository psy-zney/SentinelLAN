# Kiểm chứng tự cập nhật Agent — 2026-10-24

Phạm vi: mã nguồn updater Windows x64, chữ ký manifest và đóng gói MSI. Không bật Scheduled Task, không cài/gỡ MSI thật và không coi các kiểm thử dùng adapter giả là nghiệm thu hệ điều hành.

## Kết quả tự động

- `dotnet build apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj --no-restore --verbosity minimal -p:TreatWarningsAsErrors=true`: đạt, 0 cảnh báo và 0 lỗi.
- `dotnet format SentinelLAN.slnx whitespace --no-restore --verify-no-changes --include ...`: đạt cho các tệp updater, `Program.cs` và `AgentAutoUpdateTests.cs`.
- Bộ kiểm thử Agent cuối: **171 tổng, 161 đạt, 10 lỗi, 0 bỏ qua**. Kết quả gốc tại `.tmp-verification/agent-auto-update/agent-auto-update-final.trx` trong workspace; đây là bằng chứng máy cục bộ, không đưa tệp sinh tự động vào Git.
- Riêng **34/34** kiểm thử `AgentAutoUpdateTests` đạt trong lượt cuối. Chữ ký RSA-PSS được ký/xác minh thật bằng khóa thử nghiệm sinh trong tiến trình; HTTP, MSI và trạng thái dịch vụ dùng adapter giả. Phạm vi gồm manifest sai chữ ký/khóa/scope/hạn/version, gói sai hash/header/kích thước, từ chối redirect, chống downgrade, nonce đã dùng/đang cài, khóa độc quyền, hủy/hết hạn trước cài, kết quả MSI/phiên bản/dịch vụ và khôi phục dịch vụ khi tiến trình cài lỗi.
- Cả 10 lỗi thuộc `ExistingAgentConfigurationTests`, với `FileLoadException`: Windows Application Control chặn `SentinelLAN.Enrollment.dll`, mã `0x800711C7`. Cùng lỗi đã xuất hiện trước thay đổi updater. Không tắt hoặc né chính sách Application Control để làm bộ kiểm thử xanh.
- Parser PowerShell kiểm tra ba script triển khai và script xác minh thuộc tính MSI nhúng trong C#: đạt.

## Đóng gói

Build thử `deploy/agent/build-msi.ps1 -Version 0.0.1 -OutputDir dist/auto-update-verification`: tạo được Agent, MSI và Setup EXE self-contained. Bản thử không ký Authenticode và không ghim công ty, chỉ dùng kiểm tra đóng gói; không dùng để triển khai.

Đọc cơ sở dữ liệu MSI bằng Windows Installer COM xác nhận:

- `ProductVersion=0.0.1`, UpgradeCode `{A8F534F6-65F9-417A-B985-055721867CD4}`, template `x64;1033`.
- Có `run-windows-agent-update.ps1` và `configure-agent-updates.ps1` trong bảng File.
- `InstallInitialize=1500`, `RemoveExistingProducts=1501`, tương ứng lịch major upgrade bên trong giao dịch.

Đóng gói lại sau sửa cuối cùng để MSI/Setup chứa bản script hiện tại; checksum SHA-256 được làm mới. Kiểm tra hash nội bộ MSI của hai script so với nguồn để phát hiện bộ cài chứa script cũ.

## Nghiệm thu còn cần thực hiện

Trên máy lab được IT cho phép: cài/enroll bản MSI đã ký, bật task bằng nguồn/khóa/nhà phát hành thật, phát hành bản cao hơn, kiểm tra chạy SYSTEM tách khỏi LocalService, định danh/cấu hình còn nguyên và heartbeat báo đúng phiên bản. Thử MSI lỗi/rollback, yêu cầu reboot, mất điện giữa giao dịch, máy offline rồi online và quy trình tắt task/gỡ bản cũ. Chưa có kết quả nghiệm thu các tình huống này trong lượt sửa mã.

Tham chiếu: [hướng dẫn triển khai/gỡ](../guides/agent-auto-update.md), [ADR 0033](../adr/0033-windows-agent-auto-update.md), [WiX MajorUpgrade](https://docs.firegiant.com/wix/schema/wxs/majorupgrade/), [Windows Installer major upgrades](https://learn.microsoft.com/en-us/windows/win32/msi/major-upgrades).
