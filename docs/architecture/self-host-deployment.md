# Quyết định triển khai công ty độc lập

Ngày: 05/10/2026. Phạm vi: G1, F01/F02 của kế hoạch hoàn thiện đồ án.

Cấu hình `SENTINELLAN_DEPLOYMENT_MODE` nhận `SelfHost` hoặc `Platform` (mặc định Platform để giữ các triển khai hiện hữu). Giá trị khác làm startup thất bại thay vì vô tình mở chức năng Platform.

SelfHost bỏ đăng ký PlatformService/PlatformStore, dịch vụ SSH/VPS và route `/api/v1/platform/*`. Startup bỏ PlatformOwnerInitializer. Bootstrap công ty vẫn sử dụng cơ chế hiện có, tenant boundary và quyền Admin/Technician/Employee giữ nguyên. OpenAPI sinh từ các endpoint thực tế nên không có route Platform trong SelfHost.

Compose LAN độc lập ở `deploy/lan` chỉ gồm PostgreSQL, API, Company, Employee và TLS reverse proxy. Không tự di chuyển/xóa dữ liệu Platform cũ. Chuyển mô hình cần database mới hoặc quy trình migration dữ liệu riêng.

Kiểm chứng mã: SelfHostDeploymentTests kiểm tra GET/POST route Platform trả 404, login công ty hoạt động, không tạo Platform Owner, không đăng ký dịch vụ Platform/VPS, OpenAPI không quảng bá API bị tắt và cấu hình sai bị từ chối. Test dùng InMemory; không thay nghiệm thu PostgreSQL, TLS và hai laptop trong LAN.
