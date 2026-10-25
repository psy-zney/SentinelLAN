# Bộ dữ liệu văn phòng mẫu

Chỉ nạp khi người vận hành yêu cầu. Không tự chạy khi API khởi động.

- 12 máy và 12 nhân viên giả lập thuộc 6 phòng ban. Tên có `[MẪU]`, email dùng miền dành cho ví dụ `example.invalid`.
- Nhân viên mẫu bị khóa và có mật khẩu ngẫu nhiên không lưu lại. Không phát liên kết kích hoạt.
- Máy mẫu không có credential, token đăng ký, heartbeat hay lệnh Agent. Luôn để `LastSeenAt=null`; không giả vờ đang trực tuyến.
- 144 điểm số liệu lịch sử từ ba ngày trước, thông số tài sản và danh sách ứng dụng mô tả trong hồ sơ mẫu. Danh sách ứng dụng này **không phải** kiểm kê Agent xác nhận.
- 12 yêu cầu hỗ trợ: 7 chưa hoàn tất, 5 đã xử lý/đóng; 24 phản hồi, 10 sự cố tài sản, 6 cảnh báo và 4 lịch bảo trì.
- 2 mục danh mục dùng bộ cài MSI thật: Firefox ESR và PuTTY x64. Tệp đã kiểm tra chữ ký nhà phát hành và SHA-256 ngày 24/10/2026. Luôn cần IT duyệt; không tự cài đặt hoặc phát lệnh.

`Program.cs` dùng đúng DbContext của bản triển khai, nên nội dung hỗ trợ đi qua bộ mã hóa của ứng dụng. Mỗi bản ghi có ID xác định theo công ty/dataset, thêm trong một giao dịch và có nhật ký. Chạy lại chỉ xác minh bộ đã nạp, không ghi đè dữ liệu đang dùng.

Tại thư mục này, dùng `dotnet run --project SampleData.csproj -- validate company-sample.json` để kiểm tra cấu trúc mà không truy cập database. Hai chế độ `seed` và `verify` cần `ConnectionStrings__SentinelLAN`, `SENTINELLAN_DATA_ENCRYPTION_KEY`, `SAMPLE_ORGANIZATION_ID`, `SAMPLE_ADMIN_ID` từ môi trường riêng. `seed` còn yêu cầu `SAMPLE_CONFIRMED=true`, `SAMPLE_NONCE` là UUID mới và `SAMPLE_EXPIRES_AT` trong tối đa 10 phút. Không đưa secret vào repo, tham số lệnh hoặc log.

Bộ này giới hạn rõ công ty và Admin đã yêu cầu, không phải công cụ nhập dữ liệu tùy ý cho nhiều đơn vị. Không chỉnh ID để chạy sang công ty khác.

Trước khi thử trên máy thật: tạo tài khoản nhân viên thật, tạo mã kết nối riêng và cài Agent công ty. Cài tự động cần Windows x64, quyền phù hợp và cấu hình danh sách host tải được IT cho phép. Gói x64 không dùng cho mọi kiến trúc/hệ điều hành. Chưa nghiệm thu cài đặt trên máy thật. 7-Zip không được công bố vào danh mục vì MSI đã tải không có chữ ký Authenticode mà Agent yêu cầu.

Nguồn bộ cài: [Firefox ESR](https://www.firefox.com/en-US/download/all/desktop-esr/), [PuTTY](https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html). Phiên bản/hash/chứng thư trong fixture chỉ áp dụng cho đúng tệp đã xác minh; phải kiểm tra lại khi thay gói.
