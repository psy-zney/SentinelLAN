# Tự triển khai SentinelLAN

[README](../../README.md) là hướng dẫn cài đặt đầy đủ và được cập nhật cho mã hiện tại. Với LAN riêng, dùng [hướng dẫn self-hosted](self-hosted-guide.md); với VPS Internet, dùng [runbook Production](saas-vps-guide.md). Cả hai dùng PostgreSQL, API, web và Agent. Redis không nằm trong stack hiện tại.

## Chọn môi trường

- **Development:** `docker compose up --build` trên máy phát triển. Các cổng 3000, 8080, 5432 chỉ bind vào loopback; tài khoản demo chỉ được tạo ở môi trường Development.
- **Production:** chuẩn bị DNS, chứng chỉ TLS tin cậy, secret riêng và tổ chức/Admin bootstrap như README. Không dùng `.env.example` làm secret Production.
- **Agent:** cài trên thiết bị được ủy quyền bằng token đăng ký một lần; xem [hướng dẫn enrollment](../guides/agent-enrollment-guide.md).

## Kiểm chứng trước khi vận hành

Chạy `dotnet test SentinelLAN.slnx` và trong `apps/web` chạy `npm.cmd run lint`, `npm.cmd run typecheck`, `npm.cmd test`, `npm.cmd run build`. Từ một máy khách thật, xác nhận HTTPS, đăng nhập, enrollment, telemetry, quyền Employee theo máy được gán, khóa tài khoản, backup và restore drill. Agent giữ telemetry lỗi mạng trong bộ nhớ nên mất hàng đợi khi restart. Các lệnh khóa/cô lập máy, restart service trên Agent và nhiều lệnh khác hiện là mô phỏng; VPS SSH restart là thao tác thật với fingerprint host key đã xác minh.

Khi có dấu hiệu xâm nhập, dùng [runbook sự cố](../security/incident-workflow.md). Các giới hạn và ranh giới dữ liệu nằm ở [threat model](../security/threat-model.md).
