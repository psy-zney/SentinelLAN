# Mô hình đe dọa SentinelLAN

Đối chiếu mã nguồn tháng 10/2026. Tài sản cần bảo vệ: tài khoản **Admin/Employee**, credential Agent, RSA private key ký lệnh, khóa xác thực/mã hóa, telemetry, phiếu hỗ trợ, ảnh do nhân viên chọn và audit.

Ranh giới tin cậy: trình duyệt/mobile ↔ reverse proxy ↔ API; Agent ↔ API; API ↔ PostgreSQL; collector đặc quyền tại host ↔ file JSON cục bộ được API đọc qua mount chỉ đọc. Luồng hiện hành không có API ↔ VPS qua SSH.

Migration `20261009065038_RetireLegacyRoles` khóa role ngoài Admin/Employee, đổi security stamp, thu hồi refresh session/token kích hoạt. Technician/PlatformOwner là tên lịch sử, không phải quyền hiện hành; không tự chuyển tài khoản cũ thành Admin.

| Đe dọa | Kiểm soát đã có | Giới hạn cần vận hành |
|---|---|---|
| Giả đăng nhập / đánh cắp phiên | PBKDF2, refresh token xoay vòng, cookie HttpOnly/SameSite/Secure Production, CSRF header, security stamp và kiểm tra trạng thái/role | Bảo vệ TLS/khóa server; thu hồi credential khi có sự cố |
| Agent giả / giả lệnh / phát lại | Enrollment một lần, credential riêng từng thiết bị; RSA-PSS/SHA-256 tối thiểu 3072 bit, envelope R2 gắn key ID; private key chỉ ở server, public key ở Agent; kiểm tra device/tenant/allow-list/hạn/nonce | Bảo vệ private key, phân phối public key khi xoay khóa, đồng bộ giờ; HMAC ký lệnh chỉ fallback Development; request Agent dùng HMAC chuyên dụng theo ADR 0029 |
| Vượt tenant / dùng role đã nghỉ | Policy API, kiểm tra OrganizationId/ownership tại use case và EF global tenant filter; migration khóa/thu hồi phiên role cũ | Chưa có PostgreSQL row-level security; bootstrap/worker không có tenant phải thuộc boundary tin cậy; audit cùng DB chưa có WORM/off-site |
| Giả IP/scheme qua proxy | ForwardedHeadersMiddleware chỉ nhận X-Forwarded-For/Proto từ KnownProxies/KnownIPNetworks cấu hình; một hop, hai header có cùng số phần tử; chạy trước authentication/rate limit | Chỉ tin proxy trực tiếp/subnet dành riêng; proxy ngoài cùng ghi đè header client gửi; không trust toàn Internet hoặc publish API trực tiếp |
| Lộ telemetry / QR / ảnh | Telemetry tối thiểu; token/QR lưu hash; ảnh nhân viên chọn và đồng ý gửi, PNG/JPEG tối đa 2 MiB, tối đa 10 ảnh/yêu cầu; nội dung/tên ảnh mã hóa; danh sách chỉ Select metadata, tải ảnh kiểm tra tenant/request | Người có quyền thấy kích thước/loại ảnh; ảnh có thể chứa dữ liệu người gửi chọn; cần thời hạn lưu và bảo vệ khóa tách backup DB |
| Sửa snapshot / lạm dụng quyền host | Collector systemd tại host chạy lệnh đọc cố định, không nhận lệnh từ API; ghi file nguyên tử root:1654 mode 0640; API mount chỉ đọc, giới hạn 2 MiB, deserialize DTO; chỉ Admin đơn vị vận hành xem | Root/Docker vẫn là đặc quyền mạnh; snapshot không chống host bị chiếm. Giữ script/config root-owned, không mount Docker socket/SSH key vào API. Quá 3 phút hoặc tương lai hơn 1 phút được báo cũ |
| Quá tải / mất mạng | Rate limit, heartbeat idempotent; queue Production bảo vệ tối đa 50 mục/một giờ, retry exponential delay và jitter; danh sách ảnh không tải Content, kiểm tra số ảnh dùng COUNT | Development queue ở RAM; cần kiểm chứng restart trên OS đích, giới hạn ingress/dung lượng và restore drill; Redis backplane tùy chọn; limiter vẫn theo process, cần ingress limit và nghiệm thu failover |
| Lạm dụng lệnh hợp lệ | Allow-list, actor/tenant/device, lý do, xác nhận, hạn, nonce, chữ ký và audit; hai lệnh Simulate luôn mô phỏng; khóa/cô lập thật cần lab flag/device ID được ủy quyền | Adapter Windows/Linux có hành động thật cần nghiệm thu trên máy thử; không có API restart/reboot VPS qua SSH; nonce/receipt không bảo đảm đúng một lần qua mọi sự cố |

Trusted proxy dùng `SENTINELLAN_KNOWN_PROXIES` (IP phân cách dấu phẩy) và `SENTINELLAN_KNOWN_NETWORKS` (CIDR). Trên .NET 10, `KnownIPNetworks` thay thuộc tính `KnownNetworks` đã lỗi thời. Mặc định chỉ tin loopback; Docker cần khai báo IP/subnet thực của proxy. Xem [hướng dẫn Microsoft](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) và [triển khai](../deployment/saas-vps-guide.md).

Giảm thu thập dữ liệu không tự đáp ứng mọi luật bảo vệ dữ liệu. Đơn vị triển khai phải đặt thời hạn lưu, thông báo nhân viên và kiểm tra nghĩa vụ riêng. Test InMemory/HTTP giả lập không xác nhận PostgreSQL, TLS, native mobile hoặc hành động Windows thật. Khi có sự cố, dùng [runbook ứng phó](incident-workflow.md).

## Ranh giới mới của bản nâng cấp

Request S1 gắn method, raw target, timestamp ±60 giây, nonce PostgreSQL và body hash; khóa HMAC riêng mã hóa AAD tenant/device, đóng proof secret cũ sau request ký đầu tiên. Inventory/posture/MSI là dữ liệu host báo cáo, không phải remote attestation; Unknown không tự hồi phục alert.

Department/policy dùng foreign key composite cùng tenant, revision và mutation nonce. Bulk chỉ hai lệnh an toàn, tối đa 100 máy; enrollment batch dùng token riêng 15 phút cho từng máy và script kiểm tra bản phát hành. QR chứa secret chỉ được tạo trong browser, không gửi tới website QR bên ngoài.

SignalR Agent chỉ mang wake-up, group từ identity, kiểm tra lại quyền mỗi năm giây; outbox lưu cùng transaction và có thể phát lại. Redis phải riêng tư, TLS/auth phù hợp, prefix riêng; backplane không tự phân tán rate limiter.

S3/MinIO chỉ nhận ảnh đã mã hóa AES-GCM với AAD tenant/request/attachment; download vẫn qua API kiểm tra quyền, size và authentication tag. Offload giữ DB copy đến khi PUT thành công. Backup/đổi khóa phải bao gồm bucket. Linux chỉ apply nftables sau khi xác nhận rollback timer; cờ lab/device vẫn bắt buộc. Xem [giới hạn và cấu hình](../guides/product-upgrade.md).
