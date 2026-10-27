# Hồ sơ đồ án SentinelLAN

## Bản hiện tại

- [DOCX v3.1](SentinelLAN-Do-an-v3.1.docx) và [PDF v3.1](SentinelLAN-Do-an-v3.1.pdf): 54 trang, sáu chương, khôi phục khung bìa và logo UEH. Giữ phần tóm tắt tác giả đã sửa, không có lời cam đoan hoặc trích dẫn bản thân. Heading 1/2/3, mục lục và danh mục có dấu chấm dẫn; 85 số trang đã đối chiếu bản xuất. Bản gốc ngày 24/10/2026 được giữ riêng để truy vết.
- [Hướng dẫn nhân viên](../guides/employee-user-guide.md), kèm [DOCX](../guides/employee-user-guide.docx): báo sự cố, ảnh có đồng ý, theo dõi phản hồi, xin cài ứng dụng, hẹn IT, thông báo, QR và xin bảo trì/gỡ Agent bằng mã IT cấp.
- [Bộ ảnh web](evidence/web-2026-10-26/index.html): 41 ảnh PNG gốc, manifest SHA-256, URL, vai trò, thời điểm và dữ liệu quan sát của phiên 26/10/2026.

## Phạm vi bằng chứng

Phiên web xác nhận bàn giao SWEETYCANDY cho Employee, tạo yêu cầu có ảnh, IT tiếp nhận, biên lai CollectTelemetryNow thành công, heartbeat cập nhật và API tại commit af4fb1d. Yêu cầu hỗ trợ dừng ở InProgress, chưa nghiệm thu đóng yêu cầu. Không lấy các kết quả này để suy ra gỡ/tạm dừng Agent thực tế, cài ứng dụng, tác động OS, mobile native, camera/push hoặc khả năng chịu tải đã đạt.

Số liệu kiểm thử 23/10 và phép đo 24/10 là hồ sơ kế thừa; lần xuất báo cáo không tạo thêm kết quả kiểm thử. Thông tin nghiệm thu không chứa token, OTP, cookie hoặc khóa riêng. Mọi cấu hình production/secret tiếp tục được quản lý ngoài Git.

## Kiểm tra tài liệu

`revision-v3-check.json` và `revision-v3-pages.json` ghi kiểm tra bản v3 trước khi sửa bìa. `revision-v3.1-cover-check.json` ghi hash bản v3.1 và xác nhận phần sau bìa giữ nguyên. PDF nhúng Times New Roman. Tệp Word có trường TOC cập nhật; khi sửa nội dung cần cập nhật toàn bộ mục lục/danh mục rồi xuất lại PDF để kiểm tra phân trang.
