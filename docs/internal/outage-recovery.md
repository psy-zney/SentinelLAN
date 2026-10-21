# Xử lý mất kết nối và sự cố

Runbook nội bộ ngày 01/10/2026. Phân biệt lỗi máy/UI với tài khoản bị chiếm quyền; phân biệt process sống, API ready, SSH được và ứng dụng khỏe. Không kết luận từ dữ liệu cũ.

## Ma trận phục hồi

| Sự cố | Ảnh hưởng | Xử lý và điều kiện hoàn tất |
|---|---|---|
| Máy/trình duyệt Super Admin tắt | Dừng poll phiên đó; API/công ty/Agent có thể tiếp tục | Đăng nhập từ máy tin cậy, đọc audit và probe mới. Kiểm tra tác vụ cũ trước gửi lại. |
| Một VPS mất SSH | Node không đo/điều khiển được; node khác vẫn độc lập | Kiểm tra mạng/cổng/console nhà cung cấp/fingerprint. Hoàn tất khi pin đúng, mẫu mới và dịch vụ/app khỏe. |
| VPS chứa API/proxy sập | Login, lệnh mới, UI lỗi; SSH platform cũng không dùng được nếu API này mất | Console/SSH ngoài ứng dụng, kiểm tra máy/đĩa/mạng/proxy rồi DB/API. Hoàn tất khi readiness, login và heartbeat mới tốt. |
| PostgreSQL sập | Readiness thất bại, không ghi mutation/audit tin cậy | Bảo toàn dữ liệu, khôi phục DB/backup đã kiểm chứng riêng. Kiểm tra migration, tenant, audit và lệnh trước mở traffic. |
| Máy Admin công ty/IT sập | UI người đó dừng; Agent, ticket đã lưu có thể tiếp tục | Người cùng quyền trong đúng tenant nhận việc. Đăng nhập lại, đọc trạng thái server; không tạo bản sao ticket/lệnh đã gửi. |
| Máy nhân viên tắt/mất mạng | Agent không heartbeat, tự phục vụ gián đoạn | IT kiểm tra điện/mạng/OS qua kênh được phép. Khi phục hồi phải có heartbeat mới đúng identity và đúng người được gán. |
| UI nhân viên lỗi, Agent còn chạy | Không xem/báo được ở UI đó, telemetry có thể vẫn mới | IT kiểm tra timestamp; mở lại UI hoặc dùng kênh hỗ trợ khác. Không cài lại Agent chỉ vì UI lỗi. |

## Kết quả chưa rõ

1. Ghi node/device ID, thời điểm UTC, action và command/correlation ID; không ghi token/key vào ticket.
2. Đọc audit/receipt và trạng thái thực tế trước gửi lại. Timeout không chứng minh lệnh chưa chạy. HTTP abort không phải rollback.
3. Nonce VPS đã chấp nhận không dùng lại. Tác vụ mới chỉ tạo sau khi kiểm tra thực tế, với reason/confirmation/expiry mới.
4. Reboot chỉ lập lịch `shutdown -r +1`; `Scheduled` chưa phải boot thành công. Không click liên tiếp. Kiểm tra uptime mới, service, Docker và readiness sau khoảng chờ thực tế; không lên thì dùng console nhà cung cấp.
5. Agent ghi nonce trước chạy. Crash trước lưu receipt có thể đã chạy mà không có kết quả; không bypass nonce/ký lại để ép chạy lần hai.

## Agent offline

- Production có kho được bảo vệ, queue tối đa 50 heartbeat và một giờ; đầy thì bỏ cũ. Development có thể chỉ RAM. Không phải lưu toàn bộ telemetry offline lâu.
- Retry lỗi mạng backoff 2–60 giây có jitter; không tự chọn VPS khác. Khi kết nối lại gửi tối đa 10 mẫu/cycle, giữ idempotency key.
- Lệnh hết hạn bị từ chối; không có hành động mới chỉ do offline. Lệnh xác minh trước khi mất mạng vẫn có thể chạy: phải đọc kết quả sau phục hồi.
- Giữ tối đa một receipt chờ, gửi lại sau restart nếu còn hạn. Queue trống/nonce có mặt không chứng minh lệnh thành công.
- Không xóa identity/queue/nonce để chữa mạng. Mất identity phải Admin cấp enrollment mới, đối chiếu máy cũ tránh đăng ký trùng.

## VPS/API/DB

1. Xác định lỗi node SSH hay API chính. Node không phục vụ API sập không mặc nhiên dừng mọi công ty; API/DB dùng chung sập có thể ảnh hưởng tất cả.
2. Dùng console nhà cung cấp nếu SSH ứng dụng không dùng được. Kiểm tra VM, đĩa, mạng, đồng hồ, Docker/proxy; không reboot lặp tự động.
3. `health/live` chỉ process sống, `health/ready` phải kết nối DB. Không mở DB ra Internet.
4. Khởi động theo cấu hình triển khai đã lưu; không dùng `docker compose down -v`, prune, xóa volume hoặc DB reset để thử chữa lỗi.
5. Restore: giữ snapshot/bằng chứng lỗi; chọn backup có checksum/thời điểm rõ, chạy restore drill trên DB tách biệt trước. Giữ vault key/signing keys/cấu hình ngoài Git. DB backup đơn lẻ không đủ giải mã SSH key.
6. Restore backup cũ có thể mất nonce/receipt mới hơn backup: kiểm kê lệnh từ mốc đó, đối chiếu Agent/audit và hết hạn lệnh cũ theo quy trình. Không hứa chống replay xuyên restore khi dấu nonce đã mất.
7. Chuyển máy chủ cần DNS/TLS, DB/migration, secrets và nghiệm thu trước mở traffic. Thêm VPS vào registry không tự chuyển Agent/dữ liệu. Failover là quy trình hạ tầng riêng.

## Tự chạy sau reboot

| Thành phần | Cấu hình | Kiểm chứng |
|---|---|---|
| Dịch vụ Linux allowlist | Enable startup qua action cố định | `is-enabled`, sau reboot `is-active` |
| Docker daemon | `docker` enabled/active | `docker info` |
| Container | Policy `unless-stopped`/`always` phù hợp; Compose giữ cấu hình | State/health/policy sau reboot; container dừng chủ động có thể không chạy lại với `unless-stopped` |
| Windows Agent | MSI + script đặt auto/recovery | Service và heartbeat mới |
| Linux Agent | Installer/systemd service | Enabled/active và heartbeat mới |

Enable không start ngay; disable không stop ngay. Restart policy không sửa app lỗi/volume/DB hỏng. UI không nhận shell tùy ý.

## Nghi bị chiếm quyền

- **Admin/IT:** một Admin khác còn tin cậy khóa tài khoản, thu hồi phiên qua SecurityStamp, xem audit đúng tenant; cấp lại sau xác minh, không chia sẻ session. Nếu không còn Admin tin cậy, Super Admin tạm ngưng công ty để chặn truy cập và điều phối phục hồi ngoài ứng dụng; không bypass quyền để điều khiển endpoint.
- **Super Admin:** dùng kênh hạ tầng bảo vệ để hạn chế Platform/SSH, giữ audit, thu hồi credential/phiên và xoay SSH key bị lộ. Không có nút tự phục hồi an toàn nếu owner duy nhất bị chiếm quyền; cần xử lý ngoài ứng dụng.
- **Máy nhân viên/Agent:** Admin đúng công ty thu hồi thiết bị/credential; IT xử lý trực tiếp được phép. Không tự khóa/cô lập thật từ suy đoán; thao tác thật vẫn cần lab flag và đúng device được cho phép.
- **VPS/root/DB:** giữ log/snapshot, hạn chế traffic, khôi phục từ nguồn tin cậy, xoay keys có kế hoạch. Quyền root/DB vượt role API. Không xóa audit để làm trạng thái xanh.

## Diễn tập

Trên môi trường thử được cho phép: đóng từng UI; ngắt SSH một node; dừng API/DB; ngắt mạng Agent dưới/trên một giờ; restart Agent trong cửa sổ lệnh; reboot VPS sau startup; restore sang DB riêng. Kiểm tra tenant/node còn lại, timestamp cũ, expiry/replay/receipt/audit. Ghi thời gian phục hồi/dữ liệu mất thực tế; chưa có SLA/RPO/RTO đã nghiệm thu.
