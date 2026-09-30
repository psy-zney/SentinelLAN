# Use cases và ranh giới hành vi

Các use case lõi cùng phục vụ vòng đời một thiết bị đầu cuối được tổ chức cho phép. [Phạm vi sản phẩm](../project-overview.md) là nguồn thống nhất cho việc ưu tiên và đánh giá tính năng.

| Vai trò | Luồng đang hỗ trợ |
|---|---|
| PlatformOwner | Cấp công ty và tài khoản Admin qua link kích hoạt, theo dõi số tài khoản/thiết bị, tạm ngưng/mở lại công ty |
| Admin | Quản lý user, enrollment token, gán/thu hồi thiết bị, xem telemetry/audit, phát command có lý do và xác nhận |
| Technician | Xem thiết bị theo quyền, xử lý alert/incident/work order, thực hiện hành động được phân quyền trong tenant |
| Employee | Xem `/my-device` của máy được gán, telemetry được công bố, chính sách và incident liên quan; báo sự cố cho máy đó |
| Agent | Đăng ký bằng token một lần, gửi heartbeat idempotent, nhận command có chữ ký và gửi biên lai |

Quản trị VPS qua SSH là use case mở rộng chỉ dành cho PlatformOwner: kết nối, theo dõi tài nguyên/dịch vụ/container, cấu hình tự chạy và reboot VPS. Portal chủ hệ thống kiểm tra theo chu kỳ khi mở tab Hạ tầng; xem [hướng dẫn vận hành VPS](../deployment/vps-operations.md).

## Kịch bản cần kiểm tra

1. Admin tạo tài khoản Employee, cấp token enrollment, Agent đăng ký, Admin gán máy; Employee chỉ xem được máy được gán. Sau thu hồi, Agent credential cũ bị từ chối.
2. Token enrollment đã dùng hoặc hết hạn bị từ chối. Command sai chữ ký, hết hạn, sai thiết bị hoặc nonce đã nhận không được thực hiện. Biên lai gửi lại không tạo kết quả thứ hai.
3. Lệnh `SimulateLock`, `SimulateNetworkIsolation` chỉ mô phỏng. `RestartService` dùng adapter Windows thật khi có allow-list và quyền; thiếu điều kiện trả Failed. Khóa/cô lập thật cần lab flag và device allow-list.
4. User/thiết bị thuộc tenant khác không được đọc hoặc sửa; QR công khai chỉ trả dữ liệu tối thiểu, giải mã có xác thực phải theo tenant và assignment.
5. Audit ghi actor, target, lý do, thời gian UTC và outcome mà không lưu token, mật khẩu hoặc khóa. Bảo vệ ở tầng EF chưa thay cho kiểm soát DB và lưu trữ WORM.

Use case mở rộng VPS: restart dịch vụ, reboot có lịch và đổi cấu hình tự chạy qua SSH yêu cầu host-key pin, quyền PlatformOwner, lý do, xác nhận, nonce dùng một lần, hạn tối đa 5 phút và allow-list; đây là thao tác thật trên máy chủ được đăng ký riêng.

Các bước demo và checklist thực thi nằm trong [kịch bản demo](../demo/demo-script.md). [Thương lượng lệnh](../architecture/command-delivery.md) và [threat model](../security/threat-model.md) giải thích giới hạn bảo mật.
