> Cập nhật 27/10/2026: danh sách dưới đây đối chiếu bản gốc 24/10. Bản v3.1 và bộ 41 ảnh web ngày 26/10 đã được thêm; xem [hồ sơ hiện tại](README.md). Bằng chứng web đã bổ sung, còn mobile native và các nghiệm thu OS vẫn cần thực hiện riêng.

# Ảnh cần bổ sung cho đồ án SentinelLAN

Đối chiếu trực tiếp file `SentinelLAN-Do-an-da-sua-2026-10-24.docx` do tác giả cung cấp. File Word được lưu nguyên bản, không chỉnh nội dung hoặc cập nhật số liệu trong lần thêm này.

## Đã có trong DOCX

Có 16 tệp ảnh nhúng, gồm khung bìa/logo, sơ đồ, biểu đồ và tám ảnh giao diện demo. Các hình sau đã có ảnh thực sự, không phải ô trống:

| Hình | Nội dung | Phạm vi nguồn ghi trong báo cáo |
| --- | --- | --- |
| 5.1 | Tổng quan thiết bị trên cổng quản trị | Local, dữ liệu demo, Admin |
| 5.2 | Chi tiết thiết bị và trạng thái quan sát bảo vệ | Local, dữ liệu seed; giữ trạng thái chưa xác định |
| 5.3 | Danh sách chính sách và tình trạng áp dụng | Local, dữ liệu demo, Admin |
| 5.4 | Form phát lệnh có lý do, thời hạn và xác nhận | Local, chưa phát lệnh |
| 5.5 | Nhật ký yêu cầu hỗ trợ và hoạt động hệ thống | Local, phiên demo |
| 5.6 | Máy tính được giao trên cổng nhân viên | Local, dữ liệu demo, Employee |
| 5.7 | Mô tả sự cố và mức gián đoạn công việc | Local, yêu cầu demo |
| 5.8 | Xem trước ảnh và đồng ý trước khi gửi | Local, ảnh demo chưa gửi |

Không cần gửi lại tám ảnh này chỉ để lấp chỗ trống. Có thể thay bằng ảnh phiên bản hiện tại nếu muốn cập nhật chương giao diện, nhưng phải ghi đúng nguồn và thời điểm chụp. Các sơ đồ Hình 3.1, 3.2, 4.1–4.3 và biểu đồ Hình 6.1 cũng đã có hình.

## Cần bổ sung để chứng minh hoạt động thực tế

Đây là danh sách bằng chứng còn thiếu trong DOCX hiện tại, không phải khẳng định các kịch bản sau đã được nghiệm thu.

| Mã | Ảnh cần gửi | Nội dung phải nhìn thấy |
| --- | --- | --- |
| A01 | Setup Agent trên máy Windows được phép | Tên/phiên bản bộ cài và máy chủ công ty; che mã enrollment trước khi chụp |
| A02 | Setup báo kết nối thành công | Kết quả thành công sau đăng ký; không chỉ là mở được cửa sổ cài |
| A03 | Windows Services | `SentinelLANAgent` hoặc display name tương ứng ở trạng thái Running; nên có kiểu khởi động tự động |
| A04 | Dashboard Admin của thiết bị thật | Máy vừa cài Agent ở trạng thái Online, thời điểm heartbeat mới và telemetry thực; phân biệt dữ liệu seed |
| A05 | Cổng Employee của cùng thiết bị | Đúng máy được phân công và thông tin/telemetry tương ứng với thiết bị Admin đang xem |
| A06 | Một lệnh an toàn và biên lai thật | Ví dụ CollectTelemetryNow, có trạng thái cuối/biên lai và audit; ảnh form trước khi gửi chưa đủ chứng minh thực thi |
| A07 | Minh chứng triển khai VPS hiện tại | API ready và commit release; container/API/web đang chạy. Ảnh giám sát VPS đã gửi trong chat chưa nằm trong DOCX này; nên chụp lại sau cập nhật nếu dùng làm bằng chứng release mới |

Ưu tiên gửi A01–A04 trước: đây là khoảng trống chính của nghiệm thu cài đặt/enrollment/heartbeat, vẫn được giữ ở trạng thái chưa nghiệm thu trong trao đổi trước.

## Mobile — bổ sung nếu trình bày nghiệm thu trên điện thoại

Bản DOCX có phần mô tả mobile nhưng chưa có bộ ảnh mobile trên thiết bị thật. Cần chụp app SentinelLAN đang chạy, không chỉ màn Home của Expo Go:

- M01: đăng nhập và màn tổng quan sau đăng nhập.
- M02: chi tiết máy được giao/telemetry trên điện thoại.
- M03: quét QR và kết quả nhận diện đúng thiết bị, nếu mô tả chức năng quét QR đã nghiệm thu.
- M04: tạo yêu cầu hỗ trợ, xem trước ảnh/đồng ý gửi và kết quả tạo yêu cầu.

Ghi model điện thoại, hệ điều hành, ngày chụp, vai trò và cách chạy (Expo Go/EAS Update hoặc native build). Không lấy ảnh Expo Go làm bằng chứng push native hoạt động. Nếu chưa kiểm tra camera/push thực tế thì giữ nguyên giới hạn đó trong báo cáo.

## Minh chứng mở rộng — chỉ thu nếu muốn kết luận đã đạt

- Mất mạng/khôi phục: trạng thái offline, hàng đợi và mẫu gửi bù sau khi kết nối lại, kèm mốc thời gian/log đã loại secrets. Một ảnh đơn lẻ không chứng minh toàn bộ kịch bản.
- Khởi động lại Windows: service tự chạy và dashboard nhận heartbeat mới, kèm biên bản thời điểm.
- Khóa/cô lập mạng/khôi phục hoặc bảo trì: chỉ nghiệm thu trên máy lab được phép, có biên bản và kết quả thực tế. Không thực hiện hành động gây gián đoạn chỉ để lấy hình minh họa.
- Hiệu năng: ảnh Task Manager chỉ hỗ trợ minh họa; số CPU/RAM/độ trễ cần dữ liệu thô và phương pháp đo, không dùng ảnh dashboard hoặc dữ liệu mẫu để suy ra số đo Agent.

## Cách gửi và chèn hình

1. Gửi ảnh gốc PNG/JPG, đầy đủ cửa sổ và chữ đọc được, đặt tên theo mã A01/M01 nếu thuận tiện.
2. Kèm mô tả ngắn: môi trường, vai trò, thiết bị, thời điểm, release và bước thao tác.
3. Không để lộ token enrollment, OTP, cookie, mật khẩu, private key hoặc dữ liệu cá nhân không cần thiết. Nếu cần che, che trước khi gửi.
4. Giữ nguyên trạng thái lỗi/chưa xác định nếu đó là kết quả thật. Không dựng lại giao diện để gọi là ảnh nghiệm thu.
5. Chỉ đánh số hình mới sau khi xác định vị trí chèn; không tự đổi số Hình 5.1–5.8 đã có. Sau khi chỉnh Word phải cập nhật mục lục/danh mục hình, xuất PDF và kiểm tra bố cục theo `docs/guides/thesis-quality.md`.

## Nội dung văn bản cần rà lại khi chỉnh bản tiếp theo

Trong bảng tổng kết, DOCX vừa ghi có tám ảnh demo, vừa còn câu “Chưa có bộ ảnh giao diện được duyệt trong bản này”. Cần xác nhận ý tác giả: chưa được duyệt hay chưa có ảnh. Không nên sửa thành “đã nghiệm thu” chỉ vì ảnh đã được chèn.

Báo cáo vẫn ghi chưa xác nhận MSI/firewall/push trên thiết bị thật và cần thêm kiểm chứng PostgreSQL/TLS/mạng vật lý. Việc người dùng báo đã deploy VPS thành công có thể dùng để cập nhật phần triển khai khi có log/ảnh tương ứng; không tự chuyển toàn bộ nghiệm thu Agent/OS sang Đạt.
