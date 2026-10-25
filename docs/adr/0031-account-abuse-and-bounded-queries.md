# ADR 0031: Chống thử mật khẩu, giới hạn dữ liệu và tính nguyên tử của audit

Ngày: 2026-10-23. Trạng thái: đã triển khai trong mã nguồn, chờ xác nhận PostgreSQL và triển khai.

## Quyết định

Giới hạn đầu vào theo IP và số yêu cầu đồng thời chạy trước authentication để giảm chi phí xác thực/DB. Sau authentication có thêm giới hạn theo tenant và user/device. Chỉ tin forwarded headers từ proxy được cấu hình; gateway LAN có IP cố định riêng.

Web/mobile dùng chung bucket đăng nhập theo SHA256 của mã công ty/email đã chuẩn hóa. PostgreSQL lưu bucket và khóa `pg_advisory_xact_lock` trước đọc/cập nhật, vì giới hạn trong RAM không đủ khi chạy nhiều replica. Sau năm lần thử được nhận, áp dụng backoff 1, 2, 4… tối đa 60 giây; bucket hết hạn sau 15 phút không nhận thêm lần thử. Từ chối không kéo dài thời hạn. Đăng nhập thành công xóa bucket. Không khóa vĩnh viễn tài khoản, không ghi mật khẩu vào bucket. Tài khoản không tồn tại vẫn thực hiện PBKDF2 giả để giảm khác biệt thời gian.

Production mặc định từ chối hash mật khẩu SHA256 cũ. Có thể mở cửa sổ chuyển đổi bằng hạn UTC tối đa bảy ngày; xác thực thành công bằng mật khẩu gốc sẽ nâng lên PBKDF2. Không thể chuyển đổi hash cũ sang PBKDF2 mà không có mật khẩu gốc. Logout-all đổi security stamp và thu hồi refresh sessions trong cùng lần lưu; handler từ chối access token có stamp cũ.

Các danh sách chính có `offset`/`limit` và lọc tenant/owner trước khi phân trang trong SQL. Tham chiếu người dùng/thiết bị/trạng thái lệnh được nạp theo lô. Health worker lấy telemetry theo một truy vấn, tối đa 1024 mẫu/thiết bị trong 32 phút; thêm functional index theo thời gian đo thực tế.

Quota ảnh tính trong khóa ghi theo tenant hiện có, trước khi lưu. Một ảnh tính tối thiểu 4 KiB để hạn chế số lượng bản ghi cực nhỏ. Mặc định 20 MiB/người/ngày UTC, 200 MiB/công ty/ngày UTC, 2 GiB/công ty đang lưu. Quota không tự xóa dữ liệu. Các mutation tài sản có nhiều lần SaveChanges được bọc transaction, bao gồm audit.

## Hệ quả và giới hạn

Cần migration `20261022175225_SecurityAndBoundedQueries` trước khi nhận traffic. Index thường tạo trong transaction và có thể khóa ghi trên bảng lớn; phải thử trên bản sao staging để chọn maintenance window. Functional index được quản lý bằng SQL trong migration vì EF không biểu diễn expression index này trong model.

Giới hạn IP/identity/concurrency vẫn cục bộ trên từng process. Bucket mật khẩu và quota dùng PostgreSQL để phối hợp giữa replica. InMemory chỉ dành phát triển, không cung cấp cam kết transaction/distributed locking của PostgreSQL. Offset paging có thể dịch chuyển khi có dữ liệu mới; chưa có cursor/snapshot pagination.

Không tuyên bố ngăn được volumetric DDoS, pass-the-hash Windows/NTLM hay xâm nhập firewall từ thay đổi API này. Chúng cần kiểm thử lab và cấu hình hạ tầng riêng. Xem [hướng dẫn nghiệm thu](../security/hardening-2026-10-23.md).
