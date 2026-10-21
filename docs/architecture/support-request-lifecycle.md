# Vòng đời yêu cầu hỗ trợ

Ngày: 05/10/2026. Phạm vi: F09/F10/F14/F15, củng cố trạng thái hiện hữu.

Incident/Panic/Appointment: Open → InProgress → AwaitingEmployee hoặc Resolved → Closed. Nhân viên chỉ xác nhận đóng sau khi có kết quả/chờ xác nhận; có thể báo vẫn lỗi để mở lại từ AwaitingEmployee/Resolved/Closed, kèm lý do. Không đóng ngay yêu cầu mới bằng gọi API trực tiếp.

Loại cần phê duyệt dùng endpoint decision riêng. Không đổi Approved/Rejected bằng PATCH; không quay Approved về Open để duyệt lần nữa. Approved chưa có command phải hoàn tất bước xác nhận riêng (như OTP) trước khi chuyển xử lý. Lệnh chưa Succeeded không được chuyển AwaitingEmployee/Resolved/Closed ở cả vai trò IT và nhân viên. Reconcile không đưa yêu cầu đã Closed/Resolved về AwaitingEmployee khi đồng bộ kết quả muộn.

SupportRequestTransitions ở Domain chứa chuyển trạng thái. Application kiểm tra quyền/đối tượng/proof, trạng thái lệnh, giao dịch và audit. UI chỉ đưa ra lựa chọn phù hợp và không hiển thị nút mở lại cho thao tác cần phê duyệt. Đồng thời/ticket assignment và tách approval thành trường riêng vẫn cần rà soát tiếp theo plan; tài liệu này không tuyên bố đã hoàn thiện toàn bộ G5/G6.
