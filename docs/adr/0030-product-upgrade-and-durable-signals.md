# ADR 0030 Inventory chính sách và thông báo bền vững

Trạng thái: chấp nhận ngày 21/10/2026.

Inventory/posture và đối chiếu MSI chỉ thu dữ liệu kỹ thuật được công bố; Unknown không được xem là an toàn hay không an toàn. Policy nhận diện có revision để mẫu offline không tái kích hoạt quy tắc đã sửa/tắt. Config ngưỡng là dữ liệu tenant có optimistic concurrency, nonce và audit. Preventive tạo phiếu theo UTC ngày với khóa chống trùng, không thực hiện OS tự động.

Department liên kết User/Device riêng, policy thiết bị ưu tiên mặc định phòng ban. Thay đổi policy/membership xếp RefreshPolicy trong giao dịch; bulk chỉ cho hai lệnh thu thập/cập nhật an toàn, giới hạn 100 máy, có kết quả từng máy.

Infrastructure đưa command mới và thay đổi alert vào OperationalOutboxMessage trong cùng SaveChanges. Worker phát tín hiệu Agent và UpdatesHub sau commit; thông báo Admin/push dùng dedup key theo outbox ID. Giao nhận at-least-once; tín hiệu trùng không cấp thêm quyền thực thi. Agent hub không cung cấp RPC thực thi, server tự xác định group và kiểm tra lại quyền mỗi năm giây. Poll/lease/nonce vẫn quyết định lệnh được thực hiện. Redis tùy chọn chỉ thay transport backplane, không thay transaction hay biến limiter nội bộ thành phân tán.

Ảnh S3 được offload sau khi đã lưu DB, mã hóa AAD tenant/request/attachment. Chỉ xóa bản DB sau PUT thành công và lưu pointer; key ổn định để retry, không phụ thuộc filename do người gửi đặt. Trình duyệt luôn tải qua endpoint kiểm tra tenant. Backups và quy trình xoay khóa phải bao gồm bucket; không tuyên bố bảo vệ trước host/root bị chiếm.

Enrollment batch cấp token riêng trong một giao dịch và trả gói có hash/publisher do IT cấu hình. Linux rollback được lập lịch trước thay đổi nftables; cả Windows/Linux lock/isolation vẫn yêu cầu cờ lab và device ID. Xem [hướng dẫn vận hành](../guides/product-upgrade.md) và [HMAC request](0029-agent-request-signatures.md).
