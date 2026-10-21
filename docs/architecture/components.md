# Thành phần kiến trúc

## Backend

| Project | Trách nhiệm |
|---|---|
| `SentinelLAN.Domain` | Entity, trạng thái và quy tắc nghiệp vụ thuần |
| `SentinelLAN.Application` | Use case, contract, validation và yêu cầu phân quyền; phụ thuộc Domain |
| `SentinelLAN.Infrastructure` | EF Core/PostgreSQL, hashing, encryption và adapter đọc JSON snapshot host; triển khai Application ports |
| `SentinelLAN.Api` | HTTP, authentication, policy, SignalR và composition root |

API kết nối các lớp, còn module nghiệp vụ giao tiếp qua Application contracts/events. Tenant scope được áp dụng tường minh và bổ sung EF Core global query filter cho entity có `ITenantOwned`; chưa có database row-level security. Filter đọc tenant từ context tại thời điểm truy vấn, kể cả khi EF dùng lại model hoặc xác thực diễn ra sau khi tạo context. Khi không có tenant, context hỗ trợ bootstrap, xác thực và worker tin cậy; các use case vẫn phải kiểm tra quyền và phạm vi tổ chức. Vai trò người dùng hiện hành là Admin/Employee; migration RetireLegacyRoles khóa các role cũ và thu hồi phiên. `SentinelDbContext` chặn sửa/xóa `AuditLog` qua ứng dụng; DBA và các đường ghi khác cần kiểm soát riêng.

### Tổ chức mã nguồn Backend

`SentinelLAN.Api/Program.cs` tạo builder, đăng ký dịch vụ, dựng middleware, khởi tạo dữ liệu và ánh xạ endpoint. `Extensions/` cấu hình DI/middleware; `Endpoints/` xử lý HTTP theo nghiệp vụ, gồm HostMonitoring chỉ đọc. Forwarded headers được xử lý trước authentication/rate limit và chỉ tin proxy khai báo.

Application sở hữu contract/use case theo module; Common chứa ngữ cảnh, quyền và port dùng chung. Namespace công khai vẫn là SentinelLAN.Application. Domain và Application không phụ thuộc adapter hệ điều hành hay transport.

### Tên feature Mobile

`apps/mobile/src/features/company-admin/` chứa quản lý tài khoản công ty; `it-operator/` chứa màn hình công việc IT. Các route Expo Router tiếp tục nằm trong `src/app/` và import từ feature tương ứng.

### Thư mục cục bộ

Bản thảo khóa luận được giữ ngoài repo. `scratch/` dành cho dữ liệu tạm, được Git bỏ qua và không có trong bản nộp. Ghi chú triển khai chia sẻ phải bỏ dữ liệu hạ tầng cá nhân.

## Agent

| Project | Trách nhiệm |
|---|---|
| `SentinelLAN.Agent.Core` | Vòng đời heartbeat/queue, kiểm tra chữ ký, nonce và command allow-list, không gọi API hệ điều hành |
| `SentinelLAN.Agent.Infrastructure` | HTTP, lấy mẫu CPU/RAM/disk, DPAPI/AES-GCM và file stores |
| `SentinelLAN.Agent` | Worker cấu hình Windows Service hoặc systemd và ghép Core với Infrastructure |

Production lưu tối đa 50 telemetry item trong file bảo vệ tối đa một giờ, cùng nonce store và tối đa một pending command result để retry sau restart. Development mặc định dùng bộ nhớ. Retry tăng theo cấp số nhân với điều chỉnh ngẫu nhiên nhỏ; chưa phải full jitter. Xem [giao lệnh và khôi phục](command-delivery.md).

Agent Infrastructure có adapter Windows cho thông báo, restart service, chính sách và các lệnh lab thật `LockWorkstation`/`IsolateNetwork`. Desktop companion dùng named pipe có ACL và kiểm tra phiên console để nối Windows Service với desktop. Core lấy telemetry ngay và tải policy của thiết bị qua API. Thiếu adapter/quyền/companion trả Failed. `SimulateLock` và `SimulateNetworkIsolation` vẫn chỉ mô phỏng; xem [ADR 0007](../adr/0007-real-windows-agent-actions.md) và [hướng dẫn nghiệm thu](../guides/real-agent-execution.md). Giám sát host dùng collector systemd tại host và file JSON mount chỉ đọc; API không có SSH key hoặc Docker socket, không restart/reboot host. Xem [ADR 0025](../adr/0025-host-vps-snapshot.md) và [vận hành host](../deployment/vps-operations.md).
