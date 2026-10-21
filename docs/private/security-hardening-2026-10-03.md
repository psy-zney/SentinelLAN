> Ghi chú lịch sử; địa chỉ hạ tầng đã thay bằng ví dụ. Trạng thái hiện hành xem [tổng quan](../project-overview.md) và [giám sát host](../deployment/vps-operations.md).

# SentinelLAN — mã hóa và bảo vệ triển khai

Triển khai tại `vps-cong`, origin `https://sentinel.example.com`, release `/opt/sentinellan/releases/20261002-security-180831`. Thay đổi giới hạn trong hệ thống SentinelLAN, site Nginx tương ứng và cấu hình đăng nhập SSH; các container ứng dụng khác vẫn giữ nguyên ID và tiếp tục chạy.

## Đã triển khai

- Trình duyệt/Agent → Nginx host → gateway Docker → API hoặc proxy TLS của từng web đều dùng TLS 1.3. Gateway xác minh CA và tên server ở mỗi upstream, không bật bỏ qua kiểm tra chứng chỉ.
- Next.js chỉ nghe HTTP ở `127.0.0.1:3000` trong namespace riêng của container; proxy TLS cùng namespace phục vụ cổng 3443. Đây là kết nối cục bộ giữa hai tiến trình. Không công bố HTTP của web ra bridge network hoặc Internet.
- API chỉ nghe HTTPS 8443. Kết nối PostgreSQL dùng `VerifyFull`, CA riêng, chứng chỉ client đúng tên user, mật khẩu SCRAM và channel binding bắt buộc. PostgreSQL từ chối TCP không TLS hoặc không có chứng chỉ client hợp lệ. Bảo trì/backup dùng Unix socket và xác thực SCRAM bên trong container.
- Chỉ gateway công bố `127.0.0.1:3180`, với TLS. PostgreSQL, API và các web không công bố cổng ra host.
- PostgreSQL cập nhật 18.1 → 18.6; Nginx container cập nhật 1.27 → 1.30.5; cả hai pin digest của Docker Official Images. Không nâng phiên bản major của PostgreSQL.
- Cả 9 container chạy với user không phải root, bỏ toàn bộ Linux capabilities, cấm nâng quyền và có root filesystem chỉ đọc. Các vùng ghi runtime ở tmpfs; volume database vẫn cần quyền ghi.
- Cookie đăng nhập `Secure`, `HttpOnly`, `SameSite=Strict`; các response API có `no-store`. Bổ sung HSTS, chống nhúng trang, CSP cho base/object/frame/form và giới hạn đăng nhập theo IP. Không gọi đây là CSP chặn toàn bộ XSS: chưa có nonce và chính sách script-src đầy đủ.
- Tắt access log riêng cho site/gateway/web proxy SentinelLAN, giảm log SQL và request của API, tránh ghi thêm URL nhạy cảm trong access log mới. Không xóa log lịch sử của host dùng chung.
- SSH chỉ dùng public key; cấm root và mật khẩu, giới hạn 3 lần thử, thời gian đăng nhập 30 giây, tắt X11 forwarding. Đã xác minh kết nối SSH mới và sudo từ máy chủ quản trị sau reload.

## Backup và khóa

`/opt/sentinellan/shared/backup-daily.sh` stream `pg_dump`, ký CMS SHA-384, mã hóa CMS AES-256-CBC bằng RSA-4096 OAEP/SHA-256. Chữ ký nằm trong lớp mã hóa và phải được xác minh trước khi khôi phục. Không viết dump SQL chưa mã hóa mới xuống ổ VPS. Cấu hình khôi phục, gồm server vault key, lưu vào một file `.env.cms` riêng, cũng được ký và mã hóa.

VPS chỉ giữ khóa công khai người nhận và khóa ký backup; khóa giải mã backup và khóa CA nội bộ không được đưa lên VPS. Chứng chỉ/khóa service nằm dưới `/opt/sentinellan/shared/tls/20261003`, chỉ đọc trong các container cần dùng. Khóa TLS service vẫn phải có ở server khi service hoạt động. Certificate service hết hạn sau 365 ngày; cần gia hạn trước hạn, dùng CA offline trên máy chủ quản trị. Timer `sentinellan-certificate-check` kiểm tra mỗi ngày và báo lỗi trong systemd journal khi còn dưới 30 ngày; chưa cấu hình thông báo gửi ra ngoài. Chữ ký backup lịch sử được xác minh bằng đúng signer đã pin và CA, không phụ thuộc việc chứng chỉ sau đó hết hạn; không bỏ kiểm tra chữ ký CMS.

Thư mục chủ sở hữu trên Windows: `C:\Users\admin\Documents\SentinelLAN\Security\20261003-secure`. ACL giới hạn tài khoản hiện tại và SYSTEM. Khóa CA/service sao lưu và mật khẩu mở khóa backup được bảo vệ bằng Windows DPAPI; khóa RSA backup được mã hóa bằng mật khẩu ngẫu nhiên. File chứng chỉ công khai không chứa private key.

Windows task `SentinelLAN-Encrypted-Backup` tải các file đã mã hóa qua SSH mỗi ngày 09:45 giờ máy, chỉ khi chủ tài khoản đã đăng nhập; có chạy bù khi có thể. Backup VPS chạy 02:17 UTC, tương ứng 09:17 UTC+7, có tối đa 5 phút jitter. Nếu PC tắt hoặc chưa đăng nhập, đồng bộ chưa diễn ra. Đã tải 7 file backup/cấu hình mã hóa về PC.

DPAPI phụ thuộc tài khoản và máy Windows hiện tại. Chủ sở hữu cần chạy `python export-recovery.py` trong thư mục khóa, nhập mật khẩu mới ngay trên máy mình, rồi giữ thư mục `portable-recovery` trên thiết bị offline và mật khẩu ở nơi khác. Không nhập mật khẩu vào chat. Bản export sử dụng PKCS#8 PBES2 AES-256-CBC, PBKDF2 600.000 vòng. Chưa tự xuất bản portable vì chưa có mật khẩu do chủ sở hữu chọn.

## Kiểm tra đã thực hiện

- Compose config hợp lệ; 9 container chạy, PostgreSQL healthy; 3 trang đăng nhập và readiness trả HTTP 200 qua HTTPS.
- `pg_stat_ssl` cho kết nối API báo TLS 1.3, 256 bit, đúng DN client `/CN=sentinellan`.
- Super Admin và Admin công ty đăng nhập, đọc dữ liệu đúng quyền, cookie an toàn và đăng xuất thành công. Truy cập dữ liệu không đăng nhập bị 401; cookie Admin công ty không mở được API platform (401 do dùng loại cookie riêng).
- Backup trước thay đổi, backup sau thay đổi và 3 backup cũ đã giải mã trên PC, xác minh đúng chứng chỉ ký và khôi phục đầy đủ trong container PostgreSQL riêng: 36 bảng. Cấu hình `.env.cms` cũng đã được giải mã và xác minh chữ ký trong RAM, không xuất dữ liệu bí mật. Container kiểm tra không có mạng hoặc volume production, giới hạn CPU/RAM/PID, dùng tmpfs và tự xóa sau kiểm tra. Sau khi cả 3 bản cũ được xác minh, bản `.dump` chưa mã hóa mới được xóa; giữ `.dump.cms`.
- Các container khác trên VPS giữ nguyên ID. Certbot timer và backup timer vẫn active. Không chạy audit đầy đủ hoặc pentest toàn hệ thống.

## Vận hành

Luôn dùng đủ 3 compose file để không quay lại HTTP nội bộ. Ví dụ trên VPS:

```bash
cd /opt/sentinellan/current
sudo bash deploy/vps/compose-secure.sh ps
sudo bash deploy/vps/compose-secure.sh up -d --no-build
sudo systemctl start sentinellan-backup.service
```

`/opt/sentinellan/shared/security-20261003/rollback.sh` là phương án khôi phục cấu hình cũ nếu cần; nó quay lại kết nối nội bộ HTTP, nên chỉ dùng trong tình huống phục hồi có kiểm soát. Backup mới vẫn dùng mã hóa. Chạy lại cấu hình HTTP cũ không khôi phục cấu hình SSH đã siết.

## Chưa hoàn tất

- Đang chờ chủ sở hữu chọn VPN riêng, danh sách IP được phép, hoặc giữ web public và bổ sung MFA. Cả Super Admin/IT vẫn mở được trang đăng nhập qua Internet. MFA chưa được triển khai. Nếu chọn VPN, phải bảo vệ cả API và đăng nhập của các role quản trị; chỉ chặn URL dashboard là chưa đủ.
- Volume database đang chạy chưa được mã hóa trên ổ đĩa. TLS bảo vệ dữ liệu truyền đi; CMS bảo vệ backup. Cần chuẩn bị volume/VPS mã hóa mới, vị trí giữ khóa riêng, kiểm tra khôi phục rồi chuyển dữ liệu có kế hoạch. Không tự bật LUKS với khóa đặt cùng ổ hoặc chuyển volume live khi chưa rõ cách mở khóa sau reboot.
- Ubuntu host 20.04/Nginx host 1.18/OpenSSH 8.2/OpenSSL 1.1.1 vẫn cần kế hoạch hỗ trợ cập nhật/nâng cấp. `pro status --format json` xác nhận Ubuntu Pro chưa attached; chưa có subscription ESM được kích hoạt. Hỗ trợ bảo mật chuẩn của 20.04 đã kết thúc, cần Ubuntu Pro/ESM hoặc chuyển lên bản LTS còn hỗ trợ. Phiên bản Ubuntu có thể chứa bản vá backport; số phiên bản upstream không đủ để kết luận có một lỗ hổng cụ thể. Host còn các ứng dụng khác nên chưa nâng hệ điều hành hoặc thay Nginx dùng chung.
- Agent thực trên máy nhân viên chưa được triển khai/kiểm tra trong lần này. Agent nguồn đã bắt buộc HTTPS ngoài loopback, không có bypass chứng chỉ. TLS 1.3 bằng Schannel cần Windows 11 hoặc Server 2022 trở lên; Windows 10 Agent dùng stack hiện tại sẽ không kết nối được với endpoint chỉ TLS 1.3. Trình duyệt hiện đại trên Windows 10 có thể hỗ trợ TLS 1.3 độc lập với Schannel.
- Khóa service/DB/vault cần tồn tại trong RAM của hệ thống có quyền xử lý. Người chiếm được máy đầu cuối, root hoặc hạ tầng hypervisor có thể vẫn lấy nội dung. Mã hóa không thay thế việc bảo vệ endpoint và quản lý quyền.
- TLS không giấu IP, tên miền nếu DNS/SNI lộ, thời gian, độ dài hoặc số lượng gói tin. Không thể bảo đảm “không lộ bất kỳ gói tin gì” hay “an toàn 100%”. Các bản dump cũ đã xóa vẫn có thể còn trong snapshot/sector của nhà cung cấp; không thể bảo đảm secure erase trên VPS/cloud disk bằng xóa file.

Tham chiếu: [TLS 1.3, phân tích lưu lượng](https://www.rfc-editor.org/rfc/rfc8446#appendix-E.3), [Npgsql VerifyFull và client certificates](https://www.npgsql.org/doc/security.html), [Schannel TLS hỗ trợ theo Windows](https://learn.microsoft.com/en-us/windows/win32/secauthn/protocols-in-tls-ssl--schannel-ssp-), [vòng đời Ubuntu](https://ubuntu.com/about/release-cycle).
