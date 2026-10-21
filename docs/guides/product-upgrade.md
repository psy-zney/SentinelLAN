# Vận hành các tính năng nâng cấp tháng 10 năm 2026

Phạm vi hiện hành gồm Admin, Employee và Agent. `Technician`/`PlatformOwner` đã bị khóa bởi migration RetireLegacyRoles; người phụ trách IT dùng tài khoản Admin được tổ chức cấp phép. Giám sát host chỉ đọc JSON snapshot cục bộ, không quản trị VPS qua SSH.

## Nâng cấp và chuyển đổi Agent

Sao lưu PostgreSQL và khóa mã hóa trước khi áp dụng `20261021155323_ProductUpgrade`. Migration bổ sung dữ liệu inventory, posture, phần mềm, phòng ban, cấu hình, nonce và outbox; xóa hai bảng không còn được sử dụng `VpsNodes`, `VpsActionReservations`. Down migration chỉ tạo lại bảng rỗng; muốn lấy dữ liệu cũ phải phục hồi backup. Audit không bị xóa. Kiểm tra tên phòng ban trùng hoặc dài hơn 100 ký tự trước khi nâng cấp.

Dùng ASP.NET runtime 10.0.13 trở lên. Docker API và bản publish Agent self-contained được ghim 10.0.13. SignalR 10.0.13, MessagePack 2.5.303 và AWS SDK đã được chọn để tránh các cảnh báo bảo mật của phiên bản cũ khi bổ sung thư viện.

Nâng server trước Agent. Production mặc định yêu cầu request ký HMAC; nếu cần chuyển đổi tuần tự, cấu hình `SENTINELLAN_AGENT_LEGACY_AUTH_UNTIL` bằng UTC ISO 8601, tối đa bảy ngày từ lúc khởi động. Agent mới chứng minh credential cũ một lần qua `/api/v1/agent/auth/upgrade`, lưu khóa request riêng vào identity được bảo vệ, rồi ký từng request. Sau request ký đầu tiên, bootstrap bằng secret cũ và fallback secret bị đóng trên thiết bị đó. `SENTINELLAN_AGENT_REQUIRE_SIGNED_REQUESTS=true` đóng hoàn toàn fallback.

HMAC request S1 là giao thức riêng của dự án, không phải hiện thực đầy đủ RFC 9421. Chữ ký gắn device ID, method, raw path/query, timestamp trong ±60 giây, UUID nonce dùng một lần và SHA-256 của body chính xác; body tối đa 256 KiB. Nonce lưu PostgreSQL để kiểm tra giữa nhiều instance. Khóa HMAC chuyên dụng mã hóa AES-GCM có AAD tenant/device; khóa ký lệnh vẫn là RSA-PSS 3072 bit trở lên. Không bật ghi body/header bí mật tại proxy. Xem [ADR 0029](../adr/0029-agent-request-signatures.md).

## Cấu hình máy và trạng thái bảo vệ

Agent thu thập serial, hãng, model, CPU, tổng RAM/đĩa hệ thống và IP/MAC LAN tại enrollment/startup và định kỳ; chỉ gửi địa chỉ private/link-local đang hoạt động. Windows đọc CIM, Security Center/Defender, ba profile firewall và BitLocker. Linux đọc DMI/proc/root filesystem; posture Windows/MSI trên Linux hiển thị chưa hỗ trợ. Thiếu quyền hoặc lỗi đọc là Unknown, không phải Healthy hoặc Unhealthy.

Trang chi tiết máy và `/my-device` hiển thị dữ liệu và thời điểm đo. Inventory nằm trong `SpecificationsJson.inventory`; dữ liệu thủ công được giữ, serial/hãng/model có sẵn không bị tự ghi đè. Posture quá hai phút được đánh dấu cũ. Đây là dữ liệu Agent báo cáo, không phải chứng thực phần cứng hoặc chống giả từ host bị chiếm quyền.

## Cảnh báo và bảo trì

Admin mở Cảnh báo → Cấu hình cảnh báo và bảo trì. CPU/RAM/đĩa nhận 1–100%, cảnh báo mất kết nối 2–1440 phút, thời gian duy trì 0–30 phút, tạm dừng được duyệt 5–60 phút, chu kỳ preventive 1–60 tháng và nhắc bảo hành 1–365 ngày. Mặc định lần lượt 90/90/95%, 5 phút mất kết nối, 5 phút duy trì, 15 phút tạm dừng, 6 tháng và 30 ngày.

Lưu cấu hình có revision, nonce, hạn, lý do, xác nhận và audit; server yêu cầu đánh giá lại ngay. Để demo CPU 20%, chọn duy trì 0 và dùng một mẫu mới CPU ≥20%; mẫu cũ không đủ điều kiện. Trạng thái kết nối tức thời trên bảng máy dùng mốc hai phút riêng; trường cấu hình ở đây là độ trễ tạo cảnh báo mất kết nối.

Mỗi quy tắc giữ tối đa một alert mở cho một máy, có cooldown; mẫu bảo vệ Unknown/NotSupported không kích hoạt hoặc tự đóng cảnh báo. SecurityEvent được tạo khi phát hiện vấn đề xác nhận. Alert/command tạo outbox cùng giao dịch; worker phát SignalR và thông báo trong ứng dụng cho Admin. Push mobile sử dụng outbox và đăng ký nhận thông báo hiện có; cần cấu hình dịch vụ push và nghiệm thu trên điện thoại. Thông báo có thể gửi lại; dedup ngăn nhân bản thông báo đã lưu.

Worker preventive quét theo UTC ngày, bỏ thiết bị đã thu hồi/Retired và tổ chức tạm ngưng. Máy đến kỳ từ lần preventive hoàn tất gần nhất hoặc PurchaseDate/CreatedAt, hay sắp hết bảo hành, được tạo WorkOrder Preventive Scheduled. Checkpoint ngày, khóa tự động và kiểm tra phiếu đang mở ngăn trùng. Tính năng tạo phiếu, không tự thực hiện bảo trì OS.

## Phần mềm của cơ quan

Admin khai báo ProductCode hoặc UpgradeCode UUID trong catalog MSI và danh sách quy tắc cấm tại Hỗ trợ IT → Phần mềm. Tối đa 256 quy tắc nhận diện hoạt động trên một tổ chức. Agent chỉ đối chiếu những mã được cấp, không tải danh sách ứng dụng cá nhân. Windows dùng MSI API/Registry Uninstall trong ngữ cảnh dịch vụ; ứng dụng portable hoặc cài riêng của tài khoản khác có thể không được nhận diện.

Máy và `/my-device` hiển thị đã cài, thiếu, phiên bản chưa rõ hoặc cần cập nhật; thông tin quá 30 phút/khác revision thành Unknown. Quét mỗi 15 phút, sau cài đặt hoặc khi có CollectTelemetryNow. Phần mềm cấm tạo alert/security event; chỉ mẫu NotFound mới đóng cảnh báo. Tắt quy tắc dừng kiểm tra mới; IT vẫn cần xử lý cảnh báo đã mở. Hệ thống không tự gỡ phần mềm bị cấm.

## Phòng ban và thao tác hàng loạt

Thiết bị → Phòng ban quản lý thành viên User và Device riêng biệt. Mọi tham chiếu phòng ban/chính sách kiểm tra tenant; foreign key composite bảo vệ cùng tổ chức ở database. Chính sách gán riêng máy được ưu tiên hơn mặc định phòng ban. Khi có chính sách mặc định mới hoặc máy vào phòng ban có policy, server xếp RefreshPolicy cho máy; giới hạn 1000 máy/phòng ban. Bỏ policy không tự đảo ngược OS đã áp dụng; IT kiểm tra và cấp chính sách thay thế phù hợp. Agent chỉ thay đổi OS khi quyền/cờ thực thi cho phép.

Chọn 1–100 máy trên bảng rồi gửi RefreshPolicy hoặc CollectTelemetryNow với lý do/xác nhận. API trả kết quả từng máy, không tiết lộ dữ liệu tenant khác; nonce chặn gửi lại cùng thao tác. Chưa có lệnh khóa/cô lập hoặc shell hàng loạt.

## Enrollment QR và script cài nhanh

Cấu hình `SENTINELLAN_AGENT_PUBLIC_URL`, private RSA/key ID hiện hành và các biến bản phát hành trước khi cấp gói:

| Nền tảng | Biến cấu hình |
|---|---|
| Windows | `SENTINELLAN_BOOTSTRAP_WINDOWS_URL`, `SENTINELLAN_BOOTSTRAP_WINDOWS_SHA256`, `SENTINELLAN_BOOTSTRAP_WINDOWS_PUBLISHER` |
| Linux | `SENTINELLAN_BOOTSTRAP_LINUX_URL`, `SENTINELLAN_BOOTSTRAP_LINUX_SHA256` |

URL phải HTTPS không credential/query/fragment; SHA-256 64 hex, thumbprint Windows 40 hex. Windows dùng MSI đã ký; Linux dùng executable self-contained được IT duyệt. API không tải hay chạy gói khi cấp mã.

Thiết bị → Cài Agent nhanh cấp 1–50 gói trong một giao dịch. Mỗi gói chứa token khác nhau, dùng một lần, hạn 15 phút, server URL và public key tương ứng. QR SL1 tạo trong browser, không gửi secret tới dịch vụ QR ngoài. Tải script từng máy hoặc manifest JSON riêng tư để công cụ triển khai của IT phân phối đúng một gói/máy; không chạy cùng token trên cả phòng lab.

IT kiểm tra script rồi chạy PowerShell quản trị hoặc `sudo bash <script>`. Windows chặn redirect, kiểm tra hash và Authenticode trước msiexec; Linux curl chỉ HTTPS và kiểm tra hash trước install. Không bypass ExecutionPolicy hay chứng thư TLS. Gói chứa secret có thể đọc được dù mã hóa Base64; giữ riêng và xóa sau khi dùng. QR đăng ký khác QR nhãn tài sản. Scanner mobile cho Admin xem mã SL1 còn hạn của đúng máy chủ đang cấu hình, không tự kết nối máy chủ khác; mã chỉ giữ trong bộ nhớ và ẩn khi ứng dụng ra nền/đổi màn hình/hết hạn. IT vẫn chạy bộ cài trên máy Windows/Linux đích. Bản cài hỏng/quá hạn cần gói mới; không suy thành công từ việc process installer chạy.

## SignalR và nhiều API instance

Agent kết nối WebSocket `/hubs/agent-wake` với HMAC mới cho mỗi lần kết nối. Group được server quyết định từ tenant/device đã xác thực; kết nối bị thu hồi/tổ chức bị tạm ngưng được kiểm tra mỗi năm giây và đóng khi không kiểm chứng được quyền. Tín hiệu không chứa lệnh hay secret. HTTP poll/lease 30 giây, nonce và receipt vẫn là đường giao lệnh tin cậy. Wake-up bỏ bước telemetry thường để poll sớm; các vòng thực thi vẫn nối tiếp và giữ lịch telemetry.

Độ trễ dưới một giây là mục tiêu demo trên mạng khỏe, không phải SLA. Outbox kiểm tra mỗi 250 ms, lỗi có retry; mất SignalR vẫn poll khoảng năm giây, mạng lỗi có backoff. Muốn nhiều API instance, cấu hình `SENTINELLAN_REDIS_CONNECTION` và `SENTINELLAN_REDIS_CHANNEL_PREFIX` riêng cho từng deployment. Redis phải riêng tư, có authentication/TLS phù hợp. Browser dùng negotiate/fallback cần sticky session ở load balancer; Agent WebSocket bỏ negotiate. Redis chỉ mở rộng SignalR: rate limiter hiện tại vẫn theo từng process, cần giới hạn bổ sung tại ingress. Chia sẻ PostgreSQL, các khóa/cấu hình và trust proxy đúng cho mọi instance; kiểm thử failover riêng.

## Ảnh trên S3 hoặc MinIO

Mặc định `SENTINELLAN_ATTACHMENT_STORAGE=Database`. Chọn `S3` cùng `SENTINELLAN_S3_ENDPOINT` HTTPS, `SENTINELLAN_S3_REGION`, `SENTINELLAN_S3_BUCKET`, access/secret key có quyền tối thiểu và `SENTINELLAN_S3_FORCE_PATH_STYLE=true` cho MinIO khi cần. Không publish bucket; server dùng AWS SDK, không trả URL truy cập trực tiếp cho người dùng.

Ảnh mới được lưu DB trước. Worker mỗi năm giây chuyển tối đa mười ảnh: mã hóa AES-256-GCM với AAD tenant/request/attachment, PUT lên key UUID, sau thành công mới lưu pointer và xóa Content trong cùng lần lưu DB. Lỗi/restart trước lưu pointer giữ bản DB và retry cùng key; mất dịch vụ không làm hỏng danh sách metadata. Download vẫn xác minh tenant/request và giới hạn đúng plaintext Size + 33 byte envelope. Cần giữ cấu hình object store và khóa mã hóa để đọc các ảnh đã chuyển. Backup/restore phải gồm DB, bucket và khóa; đổi khóa cần kế hoạch chuyển mã cả đối tượng trước khi loại khóa cũ. Đối tượng mồ côi sau xóa dữ liệu ngoài ứng dụng cần đối soát vận hành; không đặt TTL tự xóa ảnh đang còn tham chiếu.

## Adapter Linux

systemd restart chỉ docker/nginx/caddy và cần `SENTINELLAN_ALLOW_SERVICE_RESTART=true` cùng quyền dịch vụ. loginctl chỉ khóa đúng một phiên desktop cục bộ đang hoạt động và chỉ báo thành công khi LockedHint được xác nhận. Khóa/cô lập thật cần `SENTINELLAN_LAB_EXECUTION=true` và `SENTINELLAN_LAB_DEVICE_ID` khớp thiết bị được phép.

nftables chỉ dùng bảng `sentinellan_<command UUID>` mới, không flush ruleset. Cần root lab/CAP_NET_ADMIN được IT cấp, API IPv4 ghim khớp DNS, thời hạn 30–60 giây, quyền systemd-run. Lịch rollback phải được tạo và timer được xác nhận active trước khi apply. Chỉ giữ loopback, API và các DNS resolver IPv4 đã cấu hình; quyền truy cập sẵn có có thể hẹp hơn do ruleset của host. Không nới quyền service mặc định: bản cài vẫn chạy user sentinellan, không capability. Thiếu điều kiện trả Failed. Nghiệm thu cần máy Linux riêng được ủy quyền, console dự phòng và kiểm tra hoàn tác thực tế. Không chạy các bài thử nftables trên máy làm việc.

## Bằng chứng và giới hạn

Kiểm thử tự động xác nhận logic, API, tenant, chữ ký, retry, WebSocket và mã hóa với fake S3. InMemory không thay PostgreSQL; fake adapter không chứng minh hệ điều hành đã đổi. Ghi riêng nghiệm thu Windows WMI/AV/BitLocker, Linux loginctl/nftables/systemd, tải MSI sạch, Redis/MinIO TLS và điện thoại push. Bản thảo khóa luận phải phân biệt mã đã triển khai, test tự động đã qua và thử nghiệm thực địa chưa thực hiện; không cam kết điểm bảo vệ hoặc tuyên bố exactly-once.

Nguồn kỹ thuật: [Redis backplane của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0), [MSI enum related products](https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msienumrelatedproductsw), [SignalR advisory](https://github.com/advisories/GHSA-334f-vvp4-7g6p), [MessagePack advisory](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h), [AWS SDK advisory](https://github.com/advisories/GHSA-9cvc-h2w8-phrp).
