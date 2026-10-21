# Khôi phục phản hồi đăng ký bị mất

Ngày: 05/10/2026. Phạm vi: G2/F04/F05. Migration mới: EnrollmentResponseRecovery.

Agent tạo bí mật 32 byte ngẫu nhiên và lưu trước khi gửi HTTP. Windows Production dùng DPAPI và thư mục có ACL bảo vệ; token/URL chỉ dùng để tạo hash ràng buộc attempt, không lưu token trong file recovery. Agent gửi DeviceSecret qua HTTPS cùng token một lần. Server lưu hash credential và EnrolledDeviceId trong cùng giao dịch dùng token/tạo máy/audit.

Nếu phản hồi bị mất, gửi lại cùng token, hostname và bí mật đã lưu trước khi token hết hạn trả lại cùng DeviceId/credential; không tạo thêm máy. Chỉ biết token đã dùng, proof khác, hostname khác, token hết hạn hoặc credential/thiết bị bị thu hồi đều không phục hồi được. Phục hồi ghi audit nhưng không ghi token/secret. Token không trở lại trạng thái chưa dùng.

Sau khi danh tính chính đã lưu thành công, Worker dọn file attempt. Crash trước bước dọn được xử lý ở startup tiếp theo khi danh tính chính đã tồn tại. Mất cả identity và proof không thể khôi phục bằng hostname/MAC: IT đối chiếu, thu hồi Agent cũ và cấp token mới; giữ lịch sử thiết bị.

Nâng server/migration trước Agent. Platform legacy còn nhận request không có DeviceSecret và tạo credential ở server; luồng đó không có recovery proof. SelfHost từ chối bộ cài cũ không gửi proof. Mọi payload secret nằm trong HTTPS, file protected hoặc bộ nhớ; không thêm log request body cho route đăng ký.

Kiểm chứng: EnrollmentRecoveryTests kiểm tra mất response, secret sai/thiếu, hostname khác, hết hạn/thu hồi và đồng thời cùng proof. EnrollmentRecoveryStoreTests chứng minh file đã tồn tại trước gửi HTTP, proof giữ qua tạo lại AgentApi, đổi token tạo proof mới và cleanup sau khi identity đã lưu. Test file sử dụng store Development riêng trong temp; bảo vệ DPAPI và cài Windows sạch vẫn cần nghiệm thu lab.
