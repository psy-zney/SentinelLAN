from pathlib import Path

root = Path(__file__).resolve().parents[1]
changes = {
    "Secure Endpoint Governance": "Quản lý thiết bị và hỗ trợ nhân viên",
    "Tôi xác nhận tạo tài khoản này và chịu trách nhiệm kiểm toán.": "Tôi được phép tạo tài khoản này cho công ty.",
    "Đo và gửi telemetry ngay": "Cập nhật thông số máy",
    "Thu thập telemetry tức thời (CollectTelemetryNow)": "Cập nhật thông số máy",
    "Tên Service (Chỉ trong Allow-list an toàn)": "Dịch vụ cần khởi động lại",
    "Lý do được lưu bất biến vào nhật ký kiểm toán cùng danh tính của bạn.": "Lý do và người thực hiện sẽ được lưu trong lịch sử hoạt động.",
    "Cấp token enrollment": "Tạo mã kết nối thiết bị",
    "Token chỉ hiển thị trong phiên này. Hãy sao chép ngay; khi đóng hộp thoại sẽ mất.": "Sao chép mã trước khi đóng cửa sổ này. Mã dùng để kết nối thiết bị với công ty.",
    "Token (bí mật một lần)": "Mã kết nối (giữ riêng)",
    "Tôi xác nhận cấp token và chịu trách nhiệm kiểm toán.": "Tôi được phép cấp mã kết nối cho thiết bị này.",
    "Tôi xác nhận thay đổi phân công và chịu trách nhiệm kiểm toán.": "Tôi xác nhận giao thiết bị cho người đã chọn.",
    "Lệnh được ký số mật mã với lý do bắt buộc và kiểm toán bất biến.": "Chọn thao tác và ghi lý do. Hệ thống sẽ lưu người thực hiện và kết quả.",
    "Ký số & Gửi lệnh": "Gửi thao tác",
    "Tổng sự kiện kiểm toán": "Hoạt động đã ghi nhận",
    "Dữ liệu người dùng, cấu hình phần cứng chi tiết và telemetry sức khỏe được bảo vệ nghiêm ngặt và chỉ hiển thị sau khi xác thực danh tính.": "Đăng nhập để xem người sử dụng và thông tin chi tiết của thiết bị.",
}
for path in (root / "packages/web-ui/src/components").glob("*.tsx"):
    text = path.read_text(encoding="utf-8")
    for old, new in changes.items():
        text = text.replace(old, new)
    path.write_text(text, encoding="utf-8", newline="\n")
