# Tổng quan SentinelLAN

SentinelLAN là hệ thống quản lý và giám sát thiết bị đầu cuối được tổ chức cho phép trên LAN. Agent gửi telemetry kỹ thuật tối thiểu (trạng thái online, CPU, RAM, dung lượng đĩa, phiên bản OS/Agent); Admin, Technician và Employee dùng web/mobile để xử lý cùng một vòng đời thiết bị. Không thu thập màn hình, phím gõ, tệp cá nhân hoặc lưu lượng mạng.

## Phạm vi sản phẩm

**Luồng lõi:** Admin cấp tài khoản và token đăng ký một lần → Agent đăng ký trên máy được ủy quyền → gửi heartbeat/telemetry có khả năng retry → Admin gán máy → Employee xem máy được gán và báo sự cố → Technician/Admin xử lý trong tenant → audit ghi hành động. QR giúp xác minh tem thiết bị trong luồng này. Chính sách hiện được lưu/gán/hiển thị; Agent chưa thực thi chế độ USB hoặc khóa màn hình. Alert hiện do người có quyền tạo và quản lý; chưa có bộ quy tắc tự sinh cảnh báo từ telemetry.

**Chức năng mở rộng đã có:** quản trị VPS Linux qua SSH từ API. Đây là luồng riêng, không cần cho demo hay nghiệm thu lõi endpoint. Triển khai chính API/web trên một VPS chỉ là lựa chọn hạ tầng, không biến quản trị VPS thành mục tiêu sản phẩm.

**Ngoài phạm vi hiện tại:** rà quét dải IP, bắt hoặc phân loại gói tin, IDS/IPS, remote desktop, lệnh tùy ý và cô lập/khóa máy thật. Mobile là giao diện Employee cho thiết bị được gán, không phải MDM quản lý điện thoại. Không mô tả các chức năng này như đã triển khai hoặc là điều kiện bắt buộc để bảo vệ đồ án.

Ưu tiên phát triển tiếp theo là làm chắc luồng lõi và đo được kết quả: chạy Agent và PostgreSQL trên môi trường đích, kiểm tra mất mạng/khôi phục/idempotency, phân quyền tenant, độ trễ heartbeat và tài nguyên Agent. Chỉ mở rộng inventory hoặc cảnh báo tự động khi có use case, ranh giới dữ liệu và bài kiểm chứng rõ ràng; không gom mọi đề xuất bên ngoài thành backlog bắt buộc.

## Tiêu chí nghiệm thu lõi

| Luồng | Bằng chứng cần có |
|---|---|
| Đăng ký và thu hồi | Token một lần đăng ký được Agent thử nghiệm; token đã dùng/hết hạn bị từ chối; sau thu hồi credential cũ không gửi được heartbeat. |
| Telemetry và mất mạng | CPU/RAM/đĩa từ Agent xuất hiện trên web; ngắt mạng rồi nối lại không tạo heartbeat trùng; báo rõ giới hạn queue và thời gian mất dữ liệu nếu vượt giới hạn. |
| Phân quyền và gán máy | Employee chỉ xem máy được gán; tài khoản khác tenant không đọc hoặc sửa được máy, incident hay QR có xác thực. |
| Sự cố và audit | Employee tạo incident, Technician xử lý; hành động nhạy cảm có actor, target, thời gian UTC, lý do và kết quả, không chứa secret. |
| Lệnh an toàn | Agent từ chối lệnh sai chữ ký, hết hạn hoặc nonce lặp; biên lai gửi lại không tạo kết quả thứ hai; lệnh mô phỏng không đổi hệ điều hành. |

Kết quả unit/integration test cần đi kèm ít nhất một lần chạy trên PostgreSQL và Agent/thiết bị thử được ủy quyền trước khi tuyên bố nghiệm thu thực địa. Ứng dụng mobile cần xác nhận riêng trên Android/iOS đích cho camera, SecureStore, deep link và mạng. [Kịch bản demo](demo/demo-script.md) là trình tự trình bày các bằng chứng này; VPS qua SSH chỉ là demo mở rộng tùy chọn.

## Thành phần và ranh giới

| Thành phần | Vai trò |
|---|---|
| `apps/backend` | API ASP.NET Core, Application use cases, Domain và Infrastructure/EF Core |
| `apps/agent` | Worker thu thập telemetry, lưu credential và nhận lệnh được ký |
| `apps/web` | Dashboard Next.js, chỉ gọi backend qua typed API client |
| `apps/mobile` | Ứng dụng Employee Expo; kiểm thử native cần thiết bị hoặc emulator riêng |
| PostgreSQL | Dữ liệu tenant, thiết bị, phiên, command và audit |

Domain không phụ thuộc Infrastructure; Application sở hữu use case và contract; Infrastructure triển khai các port; API là composition root. Module trao đổi qua Application contract/event, không truy vấn bảng của module khác. [Sơ đồ bối cảnh](architecture/context.md), [thành phần](architecture/components.md) và [các ADR](adr/0002-modular-monolith.md) ghi quyết định chi tiết.

## Luồng chính

1. Admin tạo tài khoản và token enrollment một lần. Agent đăng ký trên thiết bị được ủy quyền; server lưu hash của token/credential. Admin có thể gán hoặc thu hồi thiết bị.
2. Agent gửi heartbeat và telemetry qua HTTPS. Production dùng file bảo vệ cho hàng đợi tối đa 50 mục trong một giờ; Development mặc định dùng RAM. Retry giữ nguyên idempotency key.
3. Admin/Technician có quyền phù hợp tạo command với lý do, xác nhận, thời hạn và nonce. Agent kiểm tra chữ ký HMAC, hạn và nonce; server có delivery lease và nhận biên lai idempotent. Các lệnh Agent hiện chỉ mô phỏng, kể cả `SimulateLock`, `SimulateNetworkIsolation` và `RestartService`.
4. Employee xem máy được gán và gửi incident; Technician xử lý alert, incident và work order trong tenant.
5. Trong phần mở rộng VPS, API dùng SSH với host-key pinning và khóa riêng được mã hóa tại server. Restart dịch vụ allow-list qua đường này là hành động thật, tách biệt với command của Agent.

## Giới hạn cần báo cáo

Tenant scope là kiểm tra/truy vấn tường minh, chưa phải database row-level security. Audit chặn sửa/xóa qua EF, chưa có WORM độc lập. Khóa ký command dùng chung cho API/Agent; khóa vault được server giải mã khi SSH. Kết quả test InMemory không xác nhận PostgreSQL, TLS, SSH hay backup/restore trên môi trường triển khai. Không dùng tỷ lệ hoàn thành hoặc số test cũ làm trạng thái hiện tại.

## Tài liệu theo công việc

- [Bản đồ hệ thống HTML](system-map.html) trình bày các chủ thể, bảo mật, dữ liệu truyền đi và database; [SQL PostgreSQL](database/schema.postgresql.sql) được sinh từ migrations.
- [Chạy và cài đặt](../README.md), [VPS Production](deployment/saas-vps-guide.md), [LAN](deployment/self-hosted-guide.md), [đăng ký Agent](guides/agent-enrollment-guide.md).
- [Luồng dữ liệu](architecture/data-flow.md), [giao lệnh và khôi phục](architecture/command-delivery.md), [từ điển dữ liệu](database/data-dictionary.md) và [ERD](database/erd.md).
- [Use cases](use-cases/use-cases.md), [demo và nghiệm thu](demo/demo-script.md).
- [Quyền riêng tư](security/privacy.md), [mô hình đe dọa](security/threat-model.md), [ứng phó sự cố](security/incident-workflow.md), [rủi ro phụ thuộc](security/dependency-risk-register.md).
