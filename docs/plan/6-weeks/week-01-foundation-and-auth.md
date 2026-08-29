# Tuần 1 — Nền tảng, xác thực và tenant

## Mục tiêu

Thay cơ chế token phát triển bằng phiên đăng nhập an toàn hơn, tách danh tính người dùng/Agent và khóa chặt ranh giới tenant cho mọi vai trò.

## Công việc

- [x] Rà soát schema người dùng, organization, role và permission; ghi khoảng trống tại `docs/database/schema-gap-review.md`.
- [x] Thiết kế access token ngắn hạn và refresh-token rotation; chỉ lưu hash và hỗ trợ revoke/reuse-family detection.
- [x] Thêm endpoint đăng nhập, đọc session, refresh và đăng xuất với rate limit cùng lỗi nhất quán.
- [x] Áp dụng policy rõ ràng cho Admin, Technician, Employee và Agent; Agent dùng authentication scheme riêng.
- [x] Bắt buộc tenant/assignment scope tại Application/API và kiểm thử truy cập chéo tenant.
- [x] Chuyển web sang HttpOnly cookie, role guard và trang Employee tải dữ liệu thật; không lưu token trong browser storage.
- [x] Bổ sung unit/integration test cho token hết hạn, refresh reuse, sai role, Agent credential và tenant boundary.
- [x] Bổ sung Playwright E2E cho login → Employee view → refresh → reload → logout.
- [x] Cập nhật OpenAPI security schemes/operation requirements, threat model và tài liệu xác thực.
- [x] Sửa integration factory để dùng Npgsql thật khi có connection string; CI cấp PostgreSQL service và test khẳng định provider.

## Đầu ra

- Migration refresh session hiện có; schema review không yêu cầu migration mới và không sửa migration đã merge.
- Browser flow login → refresh → logout chạy được; Employee được chuyển đúng `/my-device`.
- Bộ test authorization, tenant, Agent scheme, OpenAPI metadata và browser E2E xanh trong môi trường hiện có.

## Bằng chứng đóng mã nguồn — 2026-08-29

- Secret thiết bị không còn xuất hiện trong URL hoặc body heartbeat/poll/result.
- `/api/v1/my-device` chỉ cho Employee và luôn kết hợp `OrganizationId` với `AssignedUserId`.
- OpenAPI mô tả `accessCookie`, `refreshCookie`, `csrfHeader`, `agentDeviceId`, `agentDeviceSecret`; test parse operation requirements.
- `SentinelApiFactory` chỉ fallback InMemory khi không có connection string; CI bắt buộc Npgsql khi PostgreSQL được cấu hình.

## Điều kiện môi trường còn mở

Máy phát triển hiện không có Docker/PostgreSQL nên chưa thể ghi nhận PostgreSQL runtime cục bộ là đã chạy. Code path, migration và CI gate đã sẵn sàng; mục này được giữ màu đen/nét đứt trong HTML tổng quan cho tới khi có log CI hoặc Docker smoke test. Không ghi nhận giả một kiểm chứng chưa thực hiện.

## Tiêu chí hoàn thành

Admin và Technician chỉ truy cập dữ liệu đúng tenant; Employee chỉ xem thiết bị được gán; Agent chỉ gọi transport endpoint bằng credential thiết bị; refresh token đã dùng hoặc bị revoke không thể dùng lại.
