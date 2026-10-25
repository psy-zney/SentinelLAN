# Khôi phục kết nối Agent — 24/10/2026

## Nguyên nhân và xử lý

- MSI 0.2.24 lỗi `1939`, kết thúc `1603`, tại cấu hình recovery của Windows Installer. Bản 0.2.25 bỏ bước MSI bị lỗi; `configure-windows-agent.ps1` vẫn đặt recovery bằng `sc.exe` trước khi khởi động dịch vụ. Cài thực tế trên Windows trả `0`.
- Máy ZNEY đã có identity nên configurator khóa ô nhập để tránh đăng ký trùng. Agent chưa hoàn tất heartbeat vì backend đang chạy chưa có `/api/v1/agent/auth/upgrade` (HTTP 404).
- Đã triển khai riêng API bằng image `sentinellan-api:agent-compat-20261024`; giữ các image web và PostgreSQL đang chạy. Không xóa identity, cấp lại enrollment hoặc bật cờ tác động OS. Agent tự nâng cấp credential và gửi heartbeat bằng identity hiện có.

Image ID: `sha256:5f2b938de0f4b102e3cd4ee5f1018ef8cd983fe9957c0544288cf1f80bbc141e`.
Source archive SHA-256: `497ed1cc1bdcb93c3cef5e1b9f428b50859fc65d1cc75ede1f158fb6d963a90b`.
Nguồn là snapshot backend trong workspace, gồm các thay đổi đã có trước lượt sửa này. Fixture hồi quy đa tenant được cập nhật riêng sau khi đóng gói: chỉ fixture đó dùng cấu hình đăng nhập nhiều đơn vị; cấu hình production vẫn ghim đơn vị.

## Dữ liệu và triển khai

Ba migration đã áp dụng: `AttachmentMetadataSize`, `ProductUpgrade`, `SecurityAndBoundedQueries`. Trước migration, `VpsNodes` và `VpsActionReservations` đều có 0 dòng. Sau migration, digest của toàn bộ ID trong `Devices`, `DeviceCredentials`, `Users` giống trước cập nhật.

Trước chuyển API đã tạo backup mã hóa và ký:

- `/opt/sentinellan/shared/backups/sentinellan-20261024T120931Z.dump.cms`
- `/opt/sentinellan/shared/backups/config-20261024T120931Z.env.cms`

Thông tin image cũ, Compose overlays, bằng chứng và backup nằm trong `/opt/sentinellan/shared/deployments/agent-compat-20261024/state.json`. Overlay image hiện hành là `release-images.json` cùng thư mục. Source staging nằm tại `/opt/sentinellan/staging/agent-compat-20261024/source`; symlink source chung không được đổi sang gói chỉ chứa backend. Khôi phục code cũ sau migration phải đánh giá tương thích schema hoặc phục hồi database và cấu hình từ cùng backup; không coi đổi image đơn lẻ là phương án phục hồi đã nghiệm thu.

## Kiểm chứng thực tế

| Phạm vi | Kết quả và bằng chứng |
| --- | --- |
| Script cấu hình Windows | 4/4 ca đạt; recovery phải được đặt trước Start-Service, token tạm được xóa ở các kết quả thành công/thất bại. Service/ACL được mock trong suite này. |
| Cấp khóa và phục hồi đăng ký | 12/12 test tập trung đạt trên Windows trước lượt hồi quy rộng. |
| Backend Linux | 190 đạt, 2 PostgreSQL chuyên biệt bỏ qua, 0 lỗi / 192. [TRX](../../dist/backend-agent-compat-20261024/linux-integration-final.trx), [log](../../dist/backend-agent-compat-20261024/linux-integration-final.log). |
| PostgreSQL bản sao tách biệt | Restore dữ liệu, áp ba migration, login bằng tài khoản kiểm thử riêng, enrollment, bootstrap credential cũ, phục hồi response bootstrap, heartbeat ký 202; replay và bootstrap sau xác nhận chữ ký trả 401. Software rules 200, policy/poll 204. Mạng nội bộ chặn gửi thông báo ra ngoài. Container, network và volume bản sao đã được gỡ sau kiểm tra. |
| HTTPS bên ngoài | Ready 200; upgrade với credential giả 401; Company/Employee login page 200. [Bằng chứng](../../dist/backend-agent-compat-20261024/public-verification.json). |
| Agent Windows thật | VPS ghi nhận ZNEY có LastSeenAt mới trong hai phút và RequestSigningConfirmedAt khác null; dịch vụ Running/Automatic. [Trạng thái triển khai](../../dist/backend-agent-compat-20261024/deployment-state.json). |
| Format/diff | Folder-mode whitespace và diff whitespace đạt cho fixture C# sửa trong lượt cập nhật backend. |

## Giới hạn

Lượt hồi quy rộng trên Windows thất bại 27 ca, có Application Control chặn `SentinelLAN.Enrollment.dll` và một fixture đa tenant dùng cấu hình đăng nhập một đơn vị. Không vô hiệu chính sách Windows. Sau sửa riêng cấu hình fixture, lượt Linux cuối đạt như bảng trên; không coi kết quả Windows lỗi là đạt.

Hai test PostgreSQL chuyên biệt trong suite hồi quy vẫn bị bỏ qua; kiểm tra PostgreSQL bản sao ở trên có phạm vi riêng, không thay thế hai test đó. Chưa đọc lại biên nhận trong ProgramData vì người dùng hủy hộp thoại quyền quản trị; kết nối thật được xác nhận ở máy chủ bằng heartbeat và xác nhận chữ ký. Đây không phải nghiệm thu đầy đủ mọi thao tác OS, reboot hoặc uninstall.

Probe urllib từ IP VPS bị Cloudflare trả 403, khiến cổng kiểm tra công khai đầu tiên không đạt. Không nới Cloudflare; truy cập HTTPS từ máy Windows bên ngoài và Agent thật đã đạt. Portal được kiểm tra tải trang đăng nhập; không dùng mật khẩu bootstrap cũ để khẳng định đăng nhập tài khoản thật. Kiểm tra đăng nhập trên database bản sao dùng tài khoản riêng, không đổi mật khẩu người dùng.
