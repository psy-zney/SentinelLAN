from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
path = root / "apps/platform/src/app/page.tsx"
text = path.read_text(encoding="utf-8")
changes = {
    "công ty con": "công ty",
    "Công ty con": "Công ty",
    "Thiết bị Endpoint": "Thiết bị",
    "Tổng người dùng": "Tài khoản",
    "Admins, IT & Nhân viên": "Quản trị viên, IT và nhân viên",
    "Các công ty đang sử dụng hệ thống": "Danh sách công ty",
    "Quản lý tài khoản, kiểm tra chi tiết thiết bị, nhân sự và tình trạng sử dụng của từng công ty.": "Xem tài khoản và thiết bị của từng công ty.",
    "Tìm kiếm theo tên công ty hoặc mã tổ chức...": "Tìm theo tên hoặc mã công ty",
    "Chưa có công ty nào. Nhấn 'Cấp tài khoản công ty mới' để bắt đầu.": "Chưa có công ty. Chọn “Thêm công ty” để bắt đầu.",
    "Kết nối Agent gần nhất": "Kết nối gần nhất",
    "Thao tác kiểm tra": "Thao tác",
    "máy online": "đang kết nối",
    "Kiểm tra chi tiết máy tính, nhân sự và sự cố": "Xem tài khoản và thiết bị",
    ">Kiểm tra<": ">Xem chi tiết<",
    "Kiểm tra\n": "Xem chi tiết\n",
    "Cấp lại link kích hoạt cho Admin công ty chưa kích hoạt": "Tạo lại liên kết cho quản trị viên chưa kích hoạt tài khoản",
    "Cấp lại link": "Cấp lại lời mời",
    "Giám sát Hạ tầng VPS & Kết nối Backend": "Tình trạng hệ thống",
    "Theo dõi tình trạng vận hành của máy chủ API, cơ sở dữ liệu PostgreSQL và các node VPS kết nối qua SSH.": "Kiểm tra dữ liệu, ứng dụng và các máy chủ đã kết nối.",
    "Đang truy vấn trạng thái từ máy chủ VPS…": "Đang kiểm tra hệ thống…",
    "Cơ sở dữ liệu (Database Engine)": "Dữ liệu",
    "Độ trễ truy vấn (Latency)": "Thời gian phản hồi",
    "Trình cung cấp (Provider)": "Hệ quản trị dữ liệu",
    "Máy chủ VPS Backend": "Ứng dụng SentinelLAN",
    "Hostname máy chủ": "Tên máy chạy ứng dụng",
    ".NET Runtime": "Phiên bản .NET",
    "Thời gian chạy liên tục (Uptime)": "Đã chạy liên tục",
    "Bộ nhớ RAM tiến trình API (Working Set)": "Bộ nhớ ứng dụng sử dụng",
    "Cấp tài khoản cho công ty mới": "Thông tin công ty",
    "Tạo tổ chức mới trên hệ thống và cấp link kích hoạt cho người quản trị (Admin) của công ty.": "Thêm công ty và tạo lời mời cho quản trị viên đầu tiên.",
    "Mã công ty (duy nhất, viết thường, không dấu)": "Mã công ty (viết thường, không dấu)",
    "Tên đầy đủ của công ty": "Tên công ty",
    "Email người quản trị công ty (Admin)": "Email quản trị viên",
    "Tên hiển thị của Admin": "Tên quản trị viên",
    "Lý do cấp tài khoản (Kiểm toán bảo mật bắt buộc)": "Lý do thêm công ty",
    "Tôi xác nhận ủy quyền quản trị và khởi tạo tenant độc lập cho công ty này.": "Tôi được phép tạo công ty và tài khoản quản trị này.",
    "Đang khởi tạo công ty…": "Đang tạo công ty…",
    "Tạo công ty & Cấp link kích hoạt Admin →": "Tạo công ty",
    "Đã cấp tài khoản công ty": "Đã tạo công ty",
    "Tài khoản Admin đã được tạo thành công cho": "Đã tạo tài khoản quản trị cho",
    "Gửi link kích hoạt này cho Admin công ty qua kênh bảo mật nội bộ.": "Gửi riêng liên kết bên dưới cho quản trị viên để họ đặt mật khẩu.",
    "Link kích hoạt dùng 1 lần (Hết hạn lúc:": "Liên kết dùng một lần, hết hạn lúc",
    "Sao chép link & Đóng": "Sao chép liên kết",
    "Đã sao chép link kích hoạt vào bộ nhớ đệm!": "Đã sao chép liên kết kích hoạt.",
    "Kiểm tra chuyên sâu:": "Chi tiết công ty:",
    "Danh sách người dùng & Phân quyền": "Tài khoản",
    "Danh sách máy tính & Thiết bị": "Thiết bị",
    "Chưa có Agent nào đăng ký vào công ty này.": "Chưa có thiết bị kết nối với công ty này.",
    "Bản Agent": "Phiên bản SentinelLAN",
    "Nhân viên sử dụng": "Người sử dụng",
    '"Online" : "Offline"': '"Đang kết nối" : "Chưa kết nối"',
    "Chưa gán": "Chưa giao máy",
    "Nhật ký hoạt động kiểm toán gần nhất": "Hoạt động gần đây",
    "Lý do & Ngữ cảnh": "Lý do",
    "Đóng chi tiết kiểm tra": "Đóng",
    "Mở lại sẽ cho phép nhân sự đăng nhập và Agent của công ty kết nối bình thường.": "Nhân viên có thể đăng nhập và các thiết bị có thể kết nối trở lại.",
    "Tạm ngưng sẽ lập tức thu hồi toàn bộ phiên đăng nhập của người dùng và chặn Agent gửi dữ liệu về máy chủ.": "Mọi người trong công ty sẽ bị đăng xuất. Thiết bị cũng ngừng gửi dữ liệu về hệ thống.",
    "Lý do thực hiện thay đổi (Bắt buộc kiểm toán)": "Lý do thay đổi",
    "Cấp lại link kích hoạt Admin": "Cấp lại lời mời quản trị viên",
    "Chỉ dành cho Admin công ty chưa kích hoạt tài khoản. Link kích hoạt cũ sẽ bị thu hồi ngay lập tức và sinh ra mã mới có hạn 24 giờ.": "Dùng khi quản trị viên chưa kích hoạt tài khoản. Liên kết cũ sẽ hết hiệu lực; liên kết mới dùng được trong 24 giờ.",
    "Lý do cấp lại link (Bắt buộc kiểm toán)": "Lý do cấp lại lời mời",
    "Tôi xác nhận cấp lại liên kết kích hoạt một lần.": "Tôi xác nhận tạo lại lời mời cho quản trị viên này.",
    "Cấp lại link ngay": "Tạo lời mời mới",
    '"#34d399"': '"var(--accent)"',
}
for old, new in changes.items():
    text = text.replace(old, new)
text = re.sub(r'\s*<div className="kpi-icon"></div>', "", text)
text = text.replace('          <span></span>\n', "")
text = text.replace('{u.role}', '{({ Admin: "Quản trị viên", Technician: "Nhân viên IT", Employee: "Nhân viên" } as Record<string, string>)[u.role] ?? u.role}')
text = text.replace('{a.severity}', '{({ Critical: "Nghiêm trọng", High: "Cao", Medium: "Vừa", Low: "Thấp", Warning: "Cảnh báo", Info: "Thông tin" } as Record<string, string>)[a.severity] ?? a.severity}')
text = text.replace('{l.outcome}', '{l.outcome === "Success" ? "Hoàn tất" : l.outcome === "Failure" || l.outcome === "Failed" ? "Chưa hoàn tất" : l.outcome}')
text = text.replace('{new Date(invitation.expiresAt).toLocaleString("vi-VN")}):', '{new Date(invitation.expiresAt).toLocaleString("vi-VN")}:')
path.write_text(text, encoding="utf-8", newline="\n")

path = root / "apps/platform/src/components/vps-monitor.tsx"
text = path.read_text(encoding="utf-8")
for old, new in {
    "Không kết nối được API. Kiểm tra lại khi kết nối ổn định.": "Chưa kết nối được hệ thống. Hãy thử lại.",
    "VPS kết nối qua SSH": "Máy chủ đã kết nối",
    "Tự kiểm tra khi đang mở tab Hạ tầng. Dừng khi chuyển tab hoặc ẩn trình duyệt.": "Tự cập nhật khi bạn đang xem trang này.",
    "Chu kỳ kiểm tra": "Cập nhật mỗi",
    "Kiểm tra lại ngay": "Kiểm tra lại",
    "Kết nối VPS": "Thêm máy chủ",
    "Chưa có VPS. Chọn Thêm máy chủ để thêm máy chủ Linux được phép quản lý.": "Chưa có máy chủ. Chọn “Thêm máy chủ” để kết nối VPS của bạn.",
    "Reboot VPS": "Khởi động lại VPS",
    "SSH kết nối được": "Đang kết nối",
    "Chưa có uptime": "Chưa có thời gian hoạt động",
    "Dịch vụ và cấu hình khi VPS khởi động": "Dịch vụ trên máy chủ",
    "Docker container": "Ứng dụng Docker",
    "Container / image": "Ứng dụng / bản chạy",
    "Trạng thái / health": "Trạng thái",
    "Dung lượng ghi / image": "Dung lượng sử dụng",
    "Mạng / I/O ổ đĩa": "Dữ liệu truyền / đọc ghi",
    "Tên VPS": "Tên máy chủ",
    "Host / địa chỉ IP": "Địa chỉ IP hoặc tên miền",
    "Private key": "Khóa riêng SSH (private key)",
    "Fingerprint SSH đã xác minh": "Mã xác minh máy chủ (SHA256)",
    "VPS sẽ reboot sau khoảng 1 phút.": "VPS sẽ khởi động lại sau khoảng 1 phút.",
    "Lệnh chuẩn:": "Lệnh sẽ chạy:",
    "Chính sách khởi động": "Cách tự khởi động",
}.items():
    text = text.replace(old, new)
path.write_text(text, encoding="utf-8", newline="\n")
