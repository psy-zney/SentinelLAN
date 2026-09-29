# Thành phần kiến trúc

## Backend

| Project | Trách nhiệm |
|---|---|
| `SentinelLAN.Domain` | Entity, trạng thái và quy tắc nghiệp vụ thuần |
| `SentinelLAN.Application` | Use case, contract, validation và yêu cầu phân quyền; phụ thuộc Domain |
| `SentinelLAN.Infrastructure` | EF Core/PostgreSQL, hashing, encryption và SSH adapter mở rộng; triển khai Application ports |
| `SentinelLAN.Api` | HTTP, authentication, policy, SignalR và composition root |

API kết nối các lớp, còn module nghiệp vụ giao tiếp qua Application contracts/events. Tenant scope được áp dụng tường minh và bổ sung EF Core global query filter cho entity có `ITenantOwned`; chưa có database row-level security. Filter đọc tenant từ context tại thời điểm truy vấn, kể cả khi EF dùng lại model hoặc xác thực diễn ra sau khi tạo context. Khi không có tenant, context hỗ trợ bootstrap, xác thực và worker tin cậy; các use case vẫn phải kiểm tra quyền và phạm vi tổ chức. `PlatformService` kiểm tra quyền `PlatformOwner` trước khi gọi store: truy vấn quản trị liên công ty bỏ filter của phiên `_platform`, còn truy vấn chi tiết và thao tác với một công ty luôn giữ điều kiện `OrganizationId` tường minh. `SentinelDbContext` chặn sửa/xóa `AuditLog` qua ứng dụng; DBA và các đường ghi khác cần kiểm soát riêng.

### Tổ chức mã nguồn Backend

`SentinelLAN.Api/Program.cs` chỉ tạo builder, đăng ký dịch vụ, dựng middleware, khởi tạo dữ liệu, ánh xạ endpoint và chạy ứng dụng. `Extensions/` chứa cấu hình DI, middleware, khởi tạo và danh sách nhóm route. `Endpoints/` chứa HTTP handler theo nghiệp vụ: Auth, MobileAuth, User, Enrollment, Device, MyDevice, Agent, Command, Policy, Alert, Audit, AssetManagement, Qr, SelfService và Platform; health check cũng có file riêng. Route VPS nằm trong nhóm Platform vì chỉ chủ hệ thống được sử dụng.

`SentinelLAN.Application/` chia thành `Authentication/`, `Users/`, `Organizations/`, `Enrollment/`, `Devices/`, `Commands/`, `Policies/`, `Alerts/`, `Audit/`, `AssetManagement/`, `SelfService/`, `VpsNodes/`, `Platform/` và `Common/`. Contract đi cùng domain sở hữu; `Common/` chỉ chứa ngữ cảnh, phân quyền và các port/validation hiện còn dùng chung. Namespace công khai vẫn là `SentinelLAN.Application` để giữ tương thích. Đây là cách tổ chức mã nguồn trong cùng assembly; việc tách port `IManagementStore` theo module là thay đổi riêng. Xem [ADR 0009](../adr/0009-backend-source-organization.md).

### Tên feature Mobile

`apps/mobile/src/features/company-admin/` chứa quản lý tài khoản công ty; `it-operator/` chứa màn hình công việc IT. Các route Expo Router tiếp tục nằm trong `src/app/` và import từ feature tương ứng.

### Thư mục cục bộ

`scratch/` và `docs/private/` được `.gitignore` loại trừ khỏi repo. `scratch/` dành cho dữ liệu tạm; `docs/private/` có thể chứa ghi chú triển khai và thông tin hạ tầng nội bộ. Không dùng `git add -f` cho hai thư mục này; tài liệu dùng chung phải nằm ở các thư mục công khai tương ứng trong `docs/` và đã bỏ dữ liệu nhạy cảm.

## Agent

| Project | Trách nhiệm |
|---|---|
| `SentinelLAN.Agent.Core` | Vòng đời heartbeat/queue, kiểm tra chữ ký, nonce và command allow-list, không gọi API hệ điều hành |
| `SentinelLAN.Agent.Infrastructure` | HTTP, lấy mẫu CPU/RAM/disk, DPAPI/AES-GCM và file stores |
| `SentinelLAN.Agent` | Worker cấu hình Windows Service hoặc systemd và ghép Core với Infrastructure |

Production lưu tối đa 50 telemetry item trong file bảo vệ tối đa một giờ, cùng nonce store và tối đa một pending command result để retry sau restart. Development mặc định dùng bộ nhớ. Retry tăng theo cấp số nhân với điều chỉnh ngẫu nhiên nhỏ; chưa phải full jitter. Xem [giao lệnh và khôi phục](command-delivery.md).

Agent Infrastructure có adapter Windows cho thông báo, restart service, chính sách và các lệnh lab thật `LockWorkstation`/`IsolateNetwork`. Desktop companion dùng named pipe có ACL và kiểm tra phiên console để nối Windows Service với desktop. Core lấy telemetry ngay và tải policy của thiết bị qua API. Thiếu adapter/quyền/companion trả Failed. `SimulateLock` và `SimulateNetworkIsolation` vẫn chỉ mô phỏng; xem [ADR 0007](../adr/0007-real-windows-agent-actions.md) và [hướng dẫn nghiệm thu](../guides/real-agent-execution.md). Quản trị VPS qua SSH vẫn là adapter backend riêng.
