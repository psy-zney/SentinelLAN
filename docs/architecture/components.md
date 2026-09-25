# Thành phần kiến trúc

## Backend

| Project | Trách nhiệm |
|---|---|
| `SentinelLAN.Domain` | Entity, trạng thái và quy tắc nghiệp vụ thuần |
| `SentinelLAN.Application` | Use case, contract, validation và yêu cầu phân quyền; phụ thuộc Domain |
| `SentinelLAN.Infrastructure` | EF Core/PostgreSQL, hashing, encryption và SSH adapter mở rộng; triển khai Application ports |
| `SentinelLAN.Api` | HTTP, authentication, policy, SignalR và composition root |

API kết nối các lớp, còn module nghiệp vụ giao tiếp qua Application contracts/events. Tenant scope được áp dụng tường minh; không có EF Core global query filter hoặc database row-level security. `SentinelDbContext` chặn sửa/xóa `AuditLog` qua ứng dụng; DBA và các đường ghi khác cần kiểm soát riêng.

## Agent

| Project | Trách nhiệm |
|---|---|
| `SentinelLAN.Agent.Core` | Vòng đời heartbeat/queue, kiểm tra chữ ký, nonce và command allow-list, không gọi API hệ điều hành |
| `SentinelLAN.Agent.Infrastructure` | HTTP, lấy mẫu CPU/RAM/disk, DPAPI/AES-GCM và file stores |
| `SentinelLAN.Agent` | Worker cấu hình Windows Service hoặc systemd và ghép Core với Infrastructure |

Production lưu tối đa 50 telemetry item trong file bảo vệ tối đa một giờ, cùng nonce store và tối đa một pending command result để retry sau restart. Development mặc định dùng bộ nhớ. Retry tăng theo cấp số nhân với điều chỉnh ngẫu nhiên nhỏ; chưa phải full jitter. Xem [giao lệnh và khôi phục](command-delivery.md).

Các lệnh Agent `SimulateLock`, `SimulateNetworkIsolation`, `RestartService`, `RefreshPolicy` và `ShowNotification` hiện chỉ trả kết quả mô phỏng. Không có TrayApp, IPC desktop, real lock hay real Agent service-restart adapter trong MVP. Quản trị VPS qua SSH là adapter backend mở rộng riêng và có thể restart dịch vụ thật khi được phép; xem [bối cảnh](context.md).
