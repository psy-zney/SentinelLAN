# Kịch bản demo và checklist nghiệm thu

Demo chỉ dùng môi trường Development/lab và thiết bị được ủy quyền. Xác nhận PostgreSQL, API `health/ready`, dashboard và Agent hoạt động trước khi bắt đầu. Nếu chưa có Docker/thiết bị thật, ghi rõ phần nào chỉ được kiểm thử bằng mock hoặc InMemory.

## Luồng trình diễn

1. Admin đăng nhập bằng tài khoản demo của môi trường Development. Xem thiết bị và telemetry CPU/RAM/disk. Nếu đã chuẩn bị tenant thứ hai cho bài test, đăng nhập tài khoản tenant đó để xác nhận không thấy dữ liệu tenant đầu.
2. Admin cấp token enrollment một lần. Agent đăng ký và gửi heartbeat; thử dùng lại token hoặc token hết hạn để xác nhận bị từ chối.
3. Admin gán thiết bị cho Employee. Employee mở `/my-device`, xem dữ liệu đang thu thập và tạo incident cho chính thiết bị được gán.
4. Admin tạo lệnh `SimulateLock` với lý do và xác nhận. Agent kiểm tra chữ ký, thời hạn, nonce, trả biên lai; màn hình máy thử **không bị khóa**. Có thể trình diễn retry/idempotency bằng cùng command ID trong bài test hoặc môi trường lab.
5. Xem audit với actor, target, lý do, thời gian UTC và kết quả. Audit chỉ được chặn sửa/xóa qua ứng dụng; chưa có WORM hay kho lưu trữ độc lập.

**Demo mở rộng tùy chọn:** nếu có VPS thử nghiệm và SSH host key được xác minh qua console tin cậy, Admin có thể thử quản trị VPS và restart một dịch vụ được allow-list. Đây là đường SSH từ API tới VPS, khác với lệnh `RestartService` của Agent vốn chỉ mô phỏng. Không cần bước này để chứng minh luồng lõi endpoint.

## Checklist trước khi báo hoàn thành

- [ ] PostgreSQL và API `health/ready` hoạt động trên môi trường demo.
- [ ] Dữ liệu demo chỉ được seed trong Development; không để lộ mật khẩu/token trong log hoặc màn hình chiếu.
- [ ] Token enrollment dùng một lần, bị từ chối khi dùng lại hoặc hết hạn.
- [ ] Heartbeat giữ idempotency key; trạng thái và telemetry xuất hiện trong dashboard.
- [ ] Lệnh Agent có chữ ký, hạn và nonce; kết quả mô phỏng được nhận một lần, không đổi OS.
- [ ] Tenant khác và Employee chưa được gán không thấy thiết bị.
- [ ] Audit có actor, target, lý do, thời gian UTC và outcome; không chứa secret.
- [ ] Chạy các bài kiểm tra phù hợp trong README và ghi lại kết quả thực tế; không dùng kết quả cũ làm bằng chứng cho bản hiện tại.
