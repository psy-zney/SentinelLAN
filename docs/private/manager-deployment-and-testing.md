# Cấu hình manager.zney295.id.vn và kiểm thử

## Đã triển khai trên VPS-Cong

Ngày: 2026-10-09. Bản chạy: 20261009-admin-employee-97dba4, source archive SHA256 225c735048592de394ede28847f74ed856321f5993959792cb5275ee39bd058d. Nguồn gồm thay đổi trong workspace đã được kiểm thử; không phải bản HEAD chưa chỉnh sửa.

Admin: https://manager.zney295.id.vn/company/login. Employee: https://manager.zney295.id.vn/employee/login. Mobile/Agent: https://manager.zney295.id.vn. Bộ cài Agent dành cho công ty: https://manager.zney295.id.vn/downloads/windows-x64/SentinelLAN.Agent.msi. SHA256: 5ae4f3b804efcf0e7342afcd259dcd524ed6dcb3d8abca0f2c4509e5fc63e8fb.

Đã build ba image, thử migration và chuyển văn bản/ảnh cũ sang AES-GCM trên bản sao PostgreSQL riêng, kiểm tra ciphertext trong database và giải mã qua API. Sau đó đã sao lưu mã hóa trước khi đổi bản chạy và giữ database phục hồi riêng. Khóa cấu hình mới được sao lưu bằng CMS mã hóa và chữ ký; khóa bí mật không được in ra hoặc nhúng vào bộ cài.

API, Admin, Employee, các sidecar TLS và gateway đã nâng cấp. Platform và platform-tls đã gỡ khỏi Docker. Chỉ gateway publish 127.0.0.1:9003; PostgreSQL/API không publish host port. Đăng nhập Admin hiện tại, session, danh sách vai trò, đăng nhập mobile, endpoint platform 404 và HTTPS công khai đều đã xác nhận.

Chứng thư manager được cấp bởi Let's Encrypt, certbot.timer đang hoạt động và có hook reload riêng cho manager. Site origin chỉ nhận các CIDR Cloudflare đã lấy từ API chính thức và loopback. Triển khai này sử dụng allowlist IP và chứng thư công khai; chưa bật yêu cầu client certificate Authenticated Origin Pulls trên tài khoản Cloudflare. SSL/TLS của Cloudflare cần ở Full (strict). Địa chỉ sslip.io cũ tiếp tục trỏ tới gateway mới để chuyển đổi.

Đã chạy certbot renew --dry-run thành công. Liên kết MSI được xác nhận qua Cloudflare bằng header nhị phân MSI và checksum; route /platform/login trả 404, truy cập danh sách user khi chưa đăng nhập trả 401.

Web gọi API theo cùng origin nên cả domain mới và địa chỉ cũ hoạt động. CORS cấu hình rõ hai origin HTTPS. Bộ cài MSI chứa URL manager và public key RSA-PSS đúng key ID của bản này; đây là bản dành cho kiểm thử, không có chứng thư Authenticode. Mobile dùng API mới; chưa phát hành APK/IPA trong lượt triển khai VPS.

Bản cũ ở /opt/sentinellan/releases/20261002-vps-monitor-220227. Trạng thái, cấu hình phục hồi, bản sao lưu và các image rollback được giữ dưới /opt/sentinellan/shared/deployments/20261009-admin-employee-97dba4 với quyền hạn chế. Khi cần phục hồi phải dùng cấu hình và database trước migration, không chạy code cũ trực tiếp trên các trường dữ liệu đã mã hóa bằng bản mới.

Các phần dưới đây ghi lại hiện trạng trước triển khai và kế hoạch ban đầu.

Ngày kiểm tra SSH: 2026-10-09. VPS-Cong: 149.118.50.176, alias vps-cong, user ubuntu.

## Hiện trạng đã đọc trên VPS

Docker Engine 26.1.3, Compose 2.27.1; Docker/Nginx đang hoạt động. Nginx host và gateway đều qua nginx -t. PostgreSQL khỏe; các container SentinelLAN chạy bằng user ít quyền, root filesystem chỉ đọc. API và PostgreSQL không có host port được publish. API thực tế nghe TLS 8443; port 8080 trong docker inspect là metadata của image. Gateway publish 127.0.0.1:3180 -> 8443 và kiểm tra health/ready trả ready với xác minh CA.

Bản chạy hiện tại vẫn có platform và platform-tls, route /platform, khóa platform owner và chữ ký HMAC cũ. Chưa có biến SENTINELLAN_DATA_ENCRYPTION_KEY và COMMAND_PRIVATE_KEY_PEM. Hostname đang chạy là sentinellan.149.118.50.176.sslip.io. Cổng 9001/9002 đã dùng, 9003 chưa có listener tại thời điểm kiểm tra. Không thay cấu hình hoặc khởi động lại dịch vụ trong lượt kiểm tra SSH này.

## Chuẩn bị triển khai

1. Sao lưu PostgreSQL và cấu hình bằng quy trình backup-encrypted hiện có; kiểm tra khả năng khôi phục và giữ khóa riêng ngoài checkout. Không dùng down -v hoặc xóa dữ liệu.
2. Dùng deploy/vps/manager.env.example làm danh sách cấu hình, bảo quản file thật ở /opt/sentinellan/shared/.env, quyền 600. PUBLIC_DOMAIN=manager.zney295.id.vn; HOST_PROXY_PORT=9003. Giữ khóa access/vault hiện tại khi nâng cấp. Tạo RSA 3072 cho COMMAND_PRIVATE_KEY_PEM và key ID riêng, cung cấp public key tương ứng trong bộ cài Agent công ty. Tạo khóa dữ liệu bằng openssl rand -base64 32; không dùng khóa demo, không gửi khóa vào chat. Đưa public key vào bộ cài đã ký và kiểm thử Agent trước khi đổi signer trên VPS.
3. Có thể giữ CA nội bộ đang dùng nếu chưa hết hạn. Script generate-internal-tls.sh tạo một thư mục mới, chứng thư có hạn 90 ngày, user/cert phù hợp cho từng container. Đổi TLS_DIRECTORY và nginx CA path cùng nhau. Không ghi đè thư mục TLS cũ.
4. Cloudflare: tạo A manager -> 149.118.50.176, bật proxy màu cam; SSL/TLS Full (strict), Always Use HTTPS và minimum TLS 1.2. Cấp Origin CA certificate cho đúng hostname, lưu cert/key ngoài Git theo đường dẫn trong nginx.manager.conf.template. Bật Authenticated Origin Pulls và đặt CA origin-pull chính thức vào file cloudflare-origin-pull-ca.pem; site mới yêu cầu client cert nên phải hoàn tất bước này trước khi bật site.
5. Thêm site Nginx riêng từ nginx.manager.conf.template, thay TLS_DIRECTORY. nginx.auth-limit.conf chỉ được include một lần ở http context. Không thay default site hoặc cấu hình của dự án khác. Không sử dụng origin rule ép manager về gateway HTTP 9000. WebSocket phải được phép; không áp CAPTCHA/interactive challenge hoặc Cloudflare Access trình duyệt cho đường dẫn Agent/mobile.
6. Cache Rule cho hostname manager.zney295.id.vn: Bypass cache cho toàn hostname khi kiểm thử để tránh cache phiên, nội dung hỗ trợ và token. Sau kiểm thử chỉ cân nhắc cache static assets; API và HTML cá nhân vẫn bypass. Đây là quy tắc phía Cloudflare, repo không tự cập nhật tài khoản Cloudflare.
7. Validate Compose bằng prod + host-proxy + encrypted và --env-file /opt/sentinellan/shared/.env. Các service chỉ còn postgres, api, company, employee, company-tls, employee-tls, nginx. Sau khi kiểm thử bản mới và duyệt triển khai, nâng cấp riêng project sentinellan-prod; remove-orphans chỉ ở project này để gỡ platform cũ, không dùng prune toàn Docker. nginx -t trước reload. Việc thay database/crypto cần lịch triển khai và phương án phục hồi.
8. Mobile: EXPO_PUBLIC_API_URL và EXPO_PUBLIC_WEB_URL đều là https://manager.zney295.id.vn. Mẫu nằm ở apps/mobile/manager.env.example. Bản preview/production buộc HTTPS. Cấu hình App Links/Universal Links cần file association phù hợp bundle ID và signing certificate của bản mobile thật.

## Kiểm thử tối thiểu

- Chạy scripts/test-all.ps1 hoặc scripts/test-all.sh; test PostgreSQL phải dùng database riêng, không dùng database đang vận hành. Lint/typecheck/build cho hai web; test mobile phải chạy --runInBand trên máy này để tránh timeout do tranh CPU.
- Admin: đăng nhập, thêm Employee, activation, cấp token một lần, đăng ký Agent bằng bộ cài công ty, phân công máy, xem cảnh báo/hỗ trợ, audit.
- Employee: chỉ xem máy được giao, gửi yêu cầu/ảnh đã xem trước và đồng ý, trao đổi/xác nhận kết quả; không truy cập quản trị công ty.
- Agent: server certificate trust, xác minh chữ ký RSA/public key của công ty, expiry/nonce/replay, thu hồi credential, offline/reconnect. Thao tác thật phải giữ cờ lab tắt.
- Mobile: đăng nhập Admin/Employee, refresh rotation/reuse, SecureStore, đăng xuất, hoạt động offline và tải lại, QR qua đúng hostname.
- Tài khoản có role PlatformOwner, SuperAdmin, Technician hoặc Agent không đăng nhập/refresh qua auth người dùng. Platform API/portal trả 404. Token cũ bị từ chối.
- Kiểm tra API no-store, cookie HttpOnly/Secure/SameSite, không đưa bí mật lên URL/log. Kiểm tra plaintext hỗ trợ được mã hóa sau nâng cấp; sai khóa hoặc sửa ciphertext phải thất bại. Volume/backup vẫn cần mã hóa riêng cho metadata còn plaintext.

Tài liệu Cloudflare chính thức: https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/ ; https://developers.cloudflare.com/ssl/origin-configuration/authenticated-origin-pull/set-up/global/ ; https://developers.cloudflare.com/cache/how-to/cache-rules/settings/

## Kết quả kiểm thử trong workspace

- Backend/Agent: 276 test qua, gồm 111 integration trên provider in-memory. NuGet hiện không báo advisory. Chưa chạy migration/chuyển dữ liệu trên PostgreSQL thật vì máy local không có Docker daemon; không tạo database thử trên VPS dùng chung.
- Web: lint/typecheck/build qua cho Admin và Employee; 36 test web qua.
- Mobile: lint/typecheck và 62 test qua với --runInBand.
- Trình duyệt: 6 test qua trong lượt đầy đủ; test API failure còn lại đã qua sau khi sửa locator và chạy lại riêng. Tổng 7 test được xác nhận qua.
- Compose encrypted validate qua: chỉ có 7 service, gateway 127.0.0.1:9003, API/database không publish host port, không có platform.
- NPM audit production còn 60 mục báo: 45 high, 15 moderate, không critical. Phần lớn là các dependency mobile chịu ảnh hưởng từ 5 advisory gốc: braces, node-forge, sprintf-js, uuid, decode-uri-component. Đã nâng sharp 0.35.5, shell-quote 1.12.0, source-map-js 1.2.2. Chưa xử lý dứt điểm các dependency mobile này; không dùng audit fix --force vì đề xuất đổi Expo/RN sang phiên bản không tương thích.
- Tra cứu DNS manager trên máy local timeout; chưa xác nhận DNS/SSL của hostname mới. Cấu hình Nginx manager và environment đã chuẩn bị, chưa áp lên VPS hoặc Cloudflare.
