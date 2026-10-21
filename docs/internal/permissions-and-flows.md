# Quyền và luồng hiện hành

Cập nhật 21/10/2026. Tài liệu này thay hướng dẫn Platform/SSH cũ. RetireLegacyRoles khóa Technician/PlatformOwner, đổi security stamp và thu hồi phiên; người làm IT sử dụng Admin được tổ chức cấp quyền.

| Actor | Phạm vi |
|---|---|
| Admin | Quản trị thiết bị/user/phòng ban/policy, cấu hình ngưỡng, catalog/rule, hỗ trợ và audit trong tenant; host snapshot chỉ khi đúng đơn vị vận hành |
| Employee | Máy chưa thu hồi được giao cho mình, hỗ trợ/thông báo và dữ liệu được công bố |
| Agent | HMAC-authenticated heartbeat/baseline/poll/result của chính thiết bị; không dùng phiên người quản trị |

Authorization policies và Application use cases kiểm tra tenant/assignment; menu không thay quyền server. Device/User/Department/Policy tham chiếu cùng tổ chức. Mutations cấu hình, phòng ban, bulk và gói đăng ký có xác nhận/lý do/nonce/hạn/audit. Bulk chỉ hai lệnh cập nhật an toàn. Lệnh thật phải qua allow-list, RSA, thời hạn và nonce; bảo trì cần phê duyệt/OTP, khóa/cô lập cần lab/device opt-in.

Agent nhận wake-up không payload, rồi poll lệnh đã ký qua HTTP lease; quyền kết nối được kiểm tra lại. Alert/command dùng transactional outbox. Host collector chạy độc lập tại host và API chỉ đọc JSON read-only; không có luồng API quản trị VPS qua SSH hoặc Docker socket.

Người giữ root/DB/khóa hạ tầng có quyền mạnh hơn role web. EF tenant filter/audit append-only không bảo vệ trước việc chiếm host/database. Xem [hướng dẫn vận hành](../guides/product-upgrade.md) và [threat model](../security/threat-model.md).
