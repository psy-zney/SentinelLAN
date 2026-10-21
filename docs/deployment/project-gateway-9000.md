# Kiểm chứng gateway9000 — 10/10/2026

Yêu cầu gốc nằm trong docs/private/vps-cong-notes.md: cổng ứng dụng trên host>9000, bind loopback, qua Nginx9000. Người dùng xác nhận sửa cấu hình VPS và cập nhật sơ đồ; Mọt Mẻ Bánh không thuộc chủ tài khoản và bị loại khỏi phạm vi thay đổi.

Chuẩn hóa BeatSync9001, LiveKit9002, SentinelLAN9003, Monopoly9004, KitchenExplore9005. Gateway HTTP dùng127.0.0.1:9000 và interface VPS; TLS relay riêng chỉ bind127.0.0.2:9000, giữ HTTPS từ Nginx443 đến gateway Docker9003. Site mặc định và Mọt Mẻ Bánh giữ nguyên. Không cài module/package, không đổi Cloudflare, không hạ TLS, không mở database, không xóa volume hoặc rebuild game.

Trước thay container: hai namespace game có0 kết nối TCP đã thiết lập tới3001. Hai container được thay để cập nhật published port; digest image không đổi.11 container khác giữ ID/image/start time/restarts/state, volume và .env không đổi. Nginx master PID46596 được giữ; reload hai bước để đóng socket wildcard9000 rồi mở các địa chỉ riêng. Chỉ listener9000 tạm gián đoạn trong khoảng chuyển cấu hình; các kết nối cũ và site443 giữ hoạt động.

Nginx-t pass trước và sau; cấu hình dự kiến được kiểm tra trong bản sao chỉ chứa text config. Backup/source changes/state tại /var/backups/sentinellan-project-gateway/20261009T202927Z, root-only.

Sau chuyển: hai game healthy, loopback9004/9005 health200, gateway9000 health200, HTTPS cả hai game và health200. Manager readiness200, login200, API host ẩn danh401. Voice HTTPS200, BeatSynchealth200. Socket.IO polling handshake thành công cho cả hai game. WebSocket handshake101 qua HTTPS và HTTP9000 đạt cho cả hai game; Sec-WebSocket-Accept được kiểm tra. TLS qua127.0.0.2:9000 xác minh CA và dùng cùng chứng chỉ gateway với9003.

UI có nhánh gateway9000 và năm dự án; từng mục mở/thu gọn tên miền, đường đi, endpoint host và binding container. Trạng thái cổng dựa trên snapshot thực tế; nhãn chỉ khẳng định cổng khớp quy ước. Mọt Mẻ Bánh ghi rõ là dự án khác trên VPS. Giữ cây chi tiết service/container và xóa dữ liệu khi mất quyền.

Kiểm tra local: web lint/typecheck,42 unit tests,2 production builds,18 Python tests, E2E desktop và mobile đạt. API/backend contract không thay đổi.

Sơ đồ định tuyến mở sẵn tên/cổng của đủ13 container. SentinelLAN có gateway Docker8443, ba nhánh API8443, company-tls3443→company3000, employee-tls3443→employee3000; SQL từAPI tớiPostgreSQL5432 được đánh dấu không quaNginx. Khu vực khác cóMọt Mẻ Bánh: Nginx80→localhost3080→API3000, database và migration riêng. Cây inventory thu gọn mặc định; bấm container mở đúng chi tiết và trả focus về nút gốc khi đóng. E2E kiểm tra đủ13 nút, routeTLS, hai nhánh SQL, mediaUDP, quyền bị thu hồi, keyboard, focus và không tràn ngang ở1440/390px.
