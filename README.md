# SentinelLAN

SentinelLAN giúp tổ chức quản lý máy tính được phép tham gia hệ thống, theo dõi tình trạng kỹ thuật và xử lý yêu cầu hỗ trợ của nhân viên.

## Chức năng

- **Nhân viên:** xem máy được giao, báo sự cố và theo dõi yêu cầu hỗ trợ.
- **Admin:** quản lý tài khoản, thiết bị, cảnh báo, yêu cầu hỗ trợ và bảo trì trong công ty.
- **Tình trạng VPS:** Admin của đơn vị vận hành xem CPU/RAM/đĩa, dịch vụ, container SentinelLAN và cổng đang nghe; cập nhật mỗi phút.
- **Mobile:** ứng dụng cho Admin hoặc Employee, dùng cùng quyền với web.
- **Agent:** gửi tình trạng kết nối và số liệu kỹ thuật từ thiết bị đã đăng ký.

## Tải và cài đặt

Xem [hướng dẫn tải và cài Agent](docs/downloads.md), [trang giới thiệu](https://psy-zney.github.io/SentinelLAN/) hoặc [GitHub Releases](https://github.com/psy-zney/SentinelLAN/releases).

Windows x64 ưu tiên MSI; Linux x64 có binary và script cài dịch vụ. Chỉ có liên kết tải trực tiếp khi bản phát hành có gói tương ứng. Cần địa chỉ HTTPS và mã đăng ký một lần do Admin công ty cấp.

## Quyền riêng tư

Chỉ thu thập dữ liệu kỹ thuật phục vụ vận hành như trạng thái kết nối, CPU, RAM, dung lượng đĩa, phiên bản hệ điều hành và Agent. Ảnh đính kèm yêu cầu hỗ trợ do nhân viên tự chọn, xem trước và đồng ý gửi.

Không thu thập mật khẩu, nội dung gõ phím, webcam, âm thanh hoặc chụp màn hình ngầm. Các thao tác quản trị phụ thuộc quyền tài khoản và cấu hình thiết bị; việc cài Agent không phải sự đồng ý cho mọi thao tác.

Tài liệu công khai chỉ gồm giới thiệu và hướng dẫn tải/cài đặt. Tài liệu thiết kế, vận hành và giao diện quản trị nền tảng được lưu riêng.
