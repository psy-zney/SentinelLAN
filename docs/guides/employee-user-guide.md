# Hướng dẫn sử dụng SentinelLAN cho nhân viên

Cổng Employee: https://manager.zney295.id.vn/employee/login. Đăng nhập bằng tài khoản được cấp. Máy cần được IT đăng ký và bàn giao cho tài khoản trước khi sử dụng các yêu cầu gắn với thiết bị.

Hướng dẫn đối chiếu theo giao diện và hợp đồng hiện có trong mã nguồn. Cấu hình từng máy quyết định khả năng thực hiện cài đặt, tạm dừng hoặc gỡ Agent; có nút yêu cầu không đồng nghĩa thao tác đã hoàn tất.


## 1. Xem máy đang được giao

1. Mở Máy tính của tôi.
2. Kiểm tra tên máy, trạng thái kết nối, lần kết nối gần nhất và phiên bản SentinelLAN. Mở Thông số máy tính để xem thông tin kỹ thuật nếu cần.
3. Nếu không có máy, hiển thị sai máy hoặc dữ liệu đã cũ, báo IT để kiểm tra phân công và kết nối. Trạng thái offline chỉ cho biết thiếu cập nhật gần đây.


## 2. Báo sự cố và gửi ảnh

1. Mở Hỗ trợ IT, chọn Tôi gặp sự cố hoặc đi từ tình huống trong Hướng dẫn nhanh.
2. Chọn nhóm vấn đề: mạng, máy in, máy chậm, ứng dụng, nghi ngờ bất thường hoặc vấn đề khác.
3. Chọn Có, nhưng cần hỗ trợ hoặc Không, công việc đang bị gián đoạn; nhập mô tả ngắn và nội dung chi tiết. Nêu thời điểm lỗi, việc đang làm và thông báo lỗi nhìn thấy.
4. Nhấn Gửi yêu cầu. Mở yêu cầu vừa tạo trong Yêu cầu của tôi để xem người phụ trách và trao đổi với IT.
5. Nếu cần ảnh: chọn PNG/JPEG, mỗi ảnh không quá 2 MiB; xem trước, chọn Tôi đã xem lại ảnh và đồng ý gửi cho IT, rồi nhấn Gửi ảnh. Che thông tin cá nhân hoặc bí mật trước khi chọn ảnh.
6. Khi IT đã trả kết quả và nút xác nhận xuất hiện, chọn Đã dùng được nếu đã khắc phục; nếu chưa, chọn Vẫn còn lỗi — nhờ IT kiểm tra lại. Không cần tạo yêu cầu mới cho cùng vấn đề.


## 3. Xin gỡ SentinelLAN Agent

1. Mở Hỗ trợ IT → Tạm dừng hoặc gỡ SentinelLAN → Xin gỡ SentinelLAN.
2. Nhập lý do gỡ và xác nhận yêu cầu dành cho máy đang được giao. Nhấn Gửi yêu cầu.
3. Chờ IT phê duyệt. Khi được duyệt, IT cấp mã xác nhận tám chữ số qua kênh riêng.
4. Mở đúng yêu cầu trong Yêu cầu của tôi. Tại Mã xác nhận IT cấp, nhập mã; tích xác nhận gỡ trên đúng máy rồi chọn Xác nhận bằng mã.
5. Theo dõi kết quả thực hiện. Được duyệt chỉ là quyền cho phép, chưa chứng minh Agent đã gỡ. IT cần xác nhận dịch vụ và ứng dụng đã được gỡ trên Windows.

Mã dùng một lần, có hạn năm phút và bị khóa sau năm lần nhập sai. Mã hết hạn hoặc không hợp lệ cần IT kiểm tra/cấp lại theo quy trình, không dùng mã của máy khác.

Gỡ tự động chỉ thực hiện khi Agent có cấu hình cho phép bảo trì, quyền triển khai phù hợp và nhận diện được gói SentinelLAN đã cài. Nếu báo thiếu quyền hoặc không tìm thấy gói đã đăng ký, gửi thông báo cho IT để gỡ bằng đường quản trị. Không xóa thư mục hay dừng cưỡng bức dịch vụ để thay thế quy trình gỡ. Sau khi gỡ, máy không còn nhận hỗ trợ qua Agent; bản ghi thiết bị trên máy chủ vẫn có thể được giữ trong lịch sử.


## 4. Xin tạm dừng Agent

1. Mở Hỗ trợ IT → Tạm dừng hoặc gỡ SentinelLAN → Xin tạm dừng 15 phút.
2. Nhập lý do, xác nhận và gửi yêu cầu.
3. Chờ IT duyệt và cấp mã. Mở yêu cầu, nhập mã tám chữ số rồi chọn Xác nhận bằng mã.
4. Theo dõi kết quả. Khi được Agent chấp nhận, trạng thái bảo trì có thời hạn và tự hết hạn; kênh trạng thái vẫn được duy trì.

Nút hiện ghi 15 phút; thời lượng thực tế lấy từ cấu hình tổ chức, mặc định 15 phút. Tạm dừng không đồng nghĩa tắt dịch vụ hoặc cắt mọi kết nối với IT.


## 5. Các chức năng nhân viên khác

Cài phần mềm: Hỗ trợ IT → Phần mềm → chọn ứng dụng → Yêu cầu cài đặt. Chọn thông tin, xác nhận và gửi. Theo dõi kết quả lệnh trong yêu cầu; chỉ có ứng dụng được công ty công bố mới dùng luồng này.

Cài đặt cần quyền IT: Tại ứng dụng trong Phần mềm, chọn Cài đặt cần quyền IT. IT xem xét quyền cài cho ứng dụng đã chọn. Phê duyệt này không cấp toàn bộ quyền quản trị Windows.

Báo khẩn cấp: Chọn Tôi nghi máy bị nhiễm virus, mô tả biểu hiện và xác nhận gửi. Yêu cầu báo khẩn cho IT; chưa được coi là máy đã cô lập mạng nếu chưa có kết quả xác nhận.

Hẹn hỗ trợ: Chọn Chọn giờ IT hỗ trợ, điền thời điểm và nội dung rồi gửi. Thời gian đề nghị phải lớn hơn năm phút và trong ba mươi ngày; đây là giờ mong muốn, cần theo dõi phản hồi từ IT.

Tự khắc phục: Mở Hướng dẫn nhanh, chọn tình huống và làm theo từng bước phù hợp. Nếu vẫn lỗi, chọn Vẫn chưa được — nhờ IT để tạo yêu cầu.

Thông báo cá nhân: Mở Thông báo, đọc nội dung; chọn Xem yêu cầu để mở công việc liên quan và Đánh dấu đã đọc sau khi xem.

Thông báo công ty: Mở Thông báo công ty. Chọn Tôi đã đọc nếu cần xác nhận. Với sự cố chung có liên quan, chọn Tôi cũng bị ảnh hưởng.

Quét tem QR: Mở Quét mã QR, cho phép camera khi được hỏi rồi quét tem thiết bị. Thông tin chi tiết phụ thuộc quyền và máy được bàn giao; quét tem không tự cấp quyền quản trị.


## 6. Đọc trạng thái và xử lý lỗi thường gặp

Đã gửi/đã tiếp nhận: Yêu cầu đã được lưu, đang chờ IT xử lý.

IT đang xử lý: Có tiến trình xử lý, chưa phải kết quả đã khắc phục.

Được duyệt: IT đã chấp thuận. Yêu cầu tạm dừng/gỡ còn cần mã; thao tác trên máy còn phải được thực hiện.

Chờ xác nhận/đã xử lý: Đọc phản hồi, thử lại công việc rồi xác nhận kết quả khi nút xuất hiện.

Không thấy yêu cầu cũ: Đổi bộ lọc từ Đang xử lý sang Tất cả; kiểm tra trang tiếp theo nếu có.

Mã sai/hết hạn: Kiểm tra đúng yêu cầu và máy; liên hệ IT thay vì thử liên tục.

Lệnh lỗi/chưa xác nhận: Mở Chi tiết kết quả IT cần kiểm tra và chuyển thông báo cho IT. Không coi thao tác là đã hoàn tất.

Lỗi kết nối: Kiểm tra mạng, thử kết nối lại hoặc đăng nhập lại. Giữ yêu cầu cũ để theo dõi; nếu máy offline, IT cần kiểm tra Agent.

Phạm vi kiểm chứng: phiên web trước đã xác nhận bàn giao máy, tạo yêu cầu, gửi ảnh, IT tiếp nhận và lệnh cập nhật thông số. Tạm dừng/gỡ thực tế, cài ứng dụng và camera QR vẫn cần nghiệm thu trên thiết bị phù hợp; hướng dẫn này không bổ sung một kết quả nghiệm thu mới.
