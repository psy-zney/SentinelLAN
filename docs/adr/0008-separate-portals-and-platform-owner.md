# ADR 0008: Tách ứng dụng và chủ hệ thống

Ngày: 2026-09-29. Trạng thái: đã triển khai trong mã nguồn.

## Vấn đề

Web cũ gộp màn hình quản trị và nhân viên trong `apps/web`. `Admin` chỉ thuộc một công ty; chưa có tài khoản chủ hệ thống để cấp công ty mới. Mobile cho IT đăng nhập nhưng mở màn hình nhân viên.

## Quyết định

| Ứng dụng | Thư mục | URL trên VPS | Quyền |
|---|---|---|---|
| Chủ hệ thống | `apps/platform` | `/platform/login` | `PlatformOwner` |
| Quản trị công ty | `apps/company` | `/company/login` | `Admin`, `Technician` |
| Nhân viên | `apps/employee` | `/employee/login` | `Employee` |
| Mobile | `apps/mobile` | Expo Android/iOS | Một ứng dụng, màn hình theo quyền công ty |
| Backend | `apps/backend` | `/api/v1`, `/hubs` | API kiểm tra vai trò và công ty |
| Thành phần web dùng chung | `packages/web-ui` | Không chạy độc lập | API client, type, hook, component có kiểm tra quyền |

Ba web có entrypoint, routes, cấu hình và build độc lập. Nginx chuyển tiếp prefix nguyên vẹn; Next.js `basePath` tự thêm prefix cho Link/router. Mobile không nhận quyền `PlatformOwner`; chủ hệ thống dùng portal riêng.

`PlatformOwner` được cấp từ biến môi trường, dưới tổ chức nội bộ `_platform`. Admin công ty không thể tạo vai trò này. Cookie chủ hệ thống có tên riêng, HttpOnly, SameSite Strict, Secure ở Production, path `/api/v1/platform`. Handler chặn token chủ hệ thống ở API công ty và ngược lại. Quyền chủ hệ thống không tự động trở thành quyền đọc nội dung phiếu hay điều khiển máy của công ty.

Tạo công ty ghi đồng thời Organization, Admin chờ kích hoạt, token kích hoạt đã hash, nonce và audit. Link chỉ trả một lần, hết hạn sau 24 giờ. Cấp lại link thu hồi link cũ, chỉ áp dụng cho Admin chưa kích hoạt. Tạm ngưng công ty đổi security stamp, thu hồi refresh session và chặn web/mobile/Agent/enrollment. Mở lại yêu cầu người dùng đăng nhập mới. Nonce có unique index; thao tác chủ hệ thống bắt buộc lý do, xác nhận và hạn tối đa 5 phút.

Migration `PlatformCompanies` bổ sung trạng thái công ty và bảng nonce, giữ nguyên tài khoản và dữ liệu hiện hữu. Không tự nâng Admin cũ thành chủ hệ thống. Các module tiếp tục dùng Application contracts; việc triển khai không thay đổi quyền thực thi Agent trên hệ điều hành.

`SentinelDbContext` áp dụng global query filter cho entity có `ITenantOwned` khi phiên hiện tại có tenant. `PlatformStore` bỏ filter của tổ chức `_platform` ở các truy vấn quản trị công ty đã được `PlatformService` kiểm tra quyền chủ hệ thống; truy vấn và thao tác với một công ty vẫn lọc theo `OrganizationId` tường minh, bao gồm cấp lại lời mời và thu hồi phiên. Các portal công ty/nhân viên tiếp tục dùng filter tenant thông thường. Context không có tenant provider vẫn hỗ trợ khởi tạo dữ liệu tin cậy.

## Hệ quả

Cần chạy ba web process/container. Không dùng `apps/web` để phát triển nữa. API và PostgreSQL vẫn là một backend trên VPS. Cookie công ty dùng chung cho hai portal công ty/nhân viên; API vẫn kiểm tra vai trò, còn cookie chủ hệ thống hoàn toàn riêng. Không thể đổi prefix Next.js sau build mà không build lại.

Kiểm thử native, PostgreSQL, TLS, push và adapter Windows cần môi trường tương ứng; build frontend không phải bằng chứng các tích hợp đó đã được nghiệm thu.
