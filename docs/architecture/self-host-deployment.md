# Triển khai công ty độc lập

Đối chiếu ngày 21/10/2026. Triển khai hiện hành phục vụ Admin/Employee và Agent; các route /api/v1/platform và dịch vụ SSH/VPS không được đăng ký. Migration RetireLegacyRoles khóa role cũ, thu hồi phiên và không tự nâng quyền tài khoản. Host monitoring chỉ đọc JSON snapshot cục bộ cho Admin của đơn vị được cấu hình.

Compose LAN gồm PostgreSQL, API, Company, Employee và TLS proxy. ProductUpgrade bổ sung cấu hình/inventory/phòng ban/phần mềm/outbox và xóa hai bảng VPS cũ; sao lưu trước khi migrate. Redis SignalR và ảnh S3/MinIO là tùy chọn, mặc định một API instance và lưu ảnh DB. Xem [vận hành nâng cấp](../guides/product-upgrade.md).

SelfHostDeploymentTests xác minh route Platform không tồn tại, login công ty hoạt động và OpenAPI không quảng bá API đã nghỉ. Kiểm thử InMemory không thay nghiệm thu PostgreSQL, TLS, restore và các máy LAN thật.
