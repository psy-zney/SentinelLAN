# Xử lý mất kết nối và sự cố

Cập nhật 21/10/2026. Ghi UTC, tenant/device/command/correlation ID, không đưa token/key/nội dung cá nhân vào ticket.

| Sự cố | Xử lý và điều kiện hoàn tất |
|---|---|
| Agent mất mạng | Kiểm tra điện/mạng/OS; giữ identity, queue và pending receipt được bảo vệ; kiểm tra heartbeat mới trước kết luận |
| SignalR/Redis lỗi | Poll HTTP tiếp tục, outbox retry; xem receipt trước tạo lại lệnh; không suy timeout là chưa chạy |
| API/proxy lỗi | Dùng kênh vận hành hạ tầng ngoài ứng dụng, kiểm tra mạng/đĩa/TLS/trust proxy và readiness; API không có SSH/reboot VPS |
| PostgreSQL lỗi | Dừng ghi không tin cậy, bảo toàn DB và audit, phục hồi backup đã thử; kiểm tra migration/tenant/nonces trước mở traffic |
| S3/MinIO lỗi | Metadata vẫn đọc được; ảnh chưa offload còn DB, ảnh đã offload cần kho/khóa đúng; không bỏ kiểm tra AAD hoặc chuyển bucket công khai |
| Snapshot cũ | Kiểm tra collector host và mount read-only; snapshot không phải bằng chứng host chưa bị chiếm |
| Lock/isolation không rõ kết quả | Dùng console lab dự phòng; giữ lịch rollback, xem trạng thái OS và receipt; không vượt nonce để chạy lại |
| Lộ credential | Thu hồi thiết bị và đăng ký lại; mất HMAC identity không phục hồi bằng hostname/MAC hoặc secret bootstrap đã đóng |

Backup phải gồm PostgreSQL, bucket nếu bật, và khóa được bảo vệ riêng. Down migration không phục hồi dữ liệu hai bảng VPS đã xóa. Xoay khóa dữ liệu cần chuyển mã object store và request-key envelopes trước khi loại khóa cũ. Kiểm thử restore/rollback trên môi trường riêng, không thử phá trên máy làm việc. Xem [runbook sự cố bảo mật](../security/incident-workflow.md).
