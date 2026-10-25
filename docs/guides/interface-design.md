# Giao diện và chuyển động SentinelLAN

## Mục tiêu

Nhân viên tìm được việc cần làm ngay, đọc kết quả bằng tiếng Việt thông thường. Quản trị viên nhận ra khu vực đang mở và các nhóm công việc. Web máy tính, web điện thoại và ứng dụng Expo dùng cùng ngôn ngữ icon và phản hồi thao tác.

## Quản trị nội bộ một công ty

Trang quản trị phục vụ vận hành máy tính, hỗ trợ IT và cấp tài khoản cho đồng nghiệp. Dùng nhãn “Quản trị nội bộ” và “Tài khoản nội bộ”; không trình bày mã công ty, ngày khởi tạo đơn vị hoặc nội dung multi-tenant trong luồng sử dụng. Danh sách tài khoản không phụ thuộc API hồ sơ công ty và không suy diễn tổng số tài khoản từ một trang dữ liệu.

Hiển thị quyền Nhân viên/Quản trị viên bằng tiếng Việt; liên kết kích hoạt là bước IT cấp tài khoản nội bộ. Tài khoản đang dùng được nhận diện bằng `userId` từ phiên xác thực và không có thao tác tự khóa. Chỉ thay cách trình bày tên bootstrap “Organization Administrator”; giữ nguyên tên do người dùng đặt và dữ liệu kiểm toán. Quy tắc này cũng áp dụng cho trang tài khoản của Expo.

## Bộ thiết kế

- Màu web: chữ `#1c302d`, chữ phụ `#596d68`, nền `#f4f7f6`, bề mặt `#ffffff`, viền `#dce6e2`, hành động chính `#176b58`. Trạng thái vẫn có chữ, không chỉ dùng màu.
- Chữ web: Segoe UI Variable / Segoe UI / Arial, dùng font hệ thống hỗ trợ tiếng Việt, không tải font từ bên ngoài. Cỡ nội dung 15px; input điện thoại 16px tránh tự phóng to khi nhập.
- Bố cục căn trái: máy tính có thanh điều hướng 244px; điện thoại có menu trượt cho quản trị và ba mục dưới cùng cho nhân viên. Nội dung hỗ trợ ưu tiên ba việc: báo sự cố, xin phần mềm, xem phản hồi.
- Chỉ nút có thể thao tác mới có phản hồi nhấn. Không đưa hiệu ứng nền động, con trỏ tùy biến hoặc chuyển động liên tục vào bảng dữ liệu. Bảng nhiều cột vẫn cuộn ngang trong vùng riêng.
- Phản hồi nhấn khoảng 110–140ms; nội dung vào trang/chuyển mục 200ms; menu 240ms. Không chờ hiệu ứng kết thúc mới điều hướng, không phát lại chuyển cảnh khi dữ liệu tự cập nhật.
- Focus rõ, có liên kết bỏ qua menu, vùng chạm chính tối thiểu 44px, có khoảng trống cho thanh điều hướng và vùng an toàn. Menu Radix giữ focus bên trong, đóng bằng Escape và trả focus về nút mở.
- Mỗi trang có nút **Quay lại** với mũi tên và chữ, vùng chạm tối thiểu 48px. Trên web điện thoại, thanh quay lại giữ ở đầu khi cuộn; không yêu cầu người dùng sửa URL hoặc dùng nút trình duyệt. Company/Employee dùng cùng component; trang đăng nhập, kích hoạt và QR cũng có điều hướng. Lịch sử chỉ dùng các trang đã đi qua trong lần mở cổng hiện tại, dừng ở ranh giới đăng nhập/kích hoạt. Mở trực tiếp trang chi tiết thiết bị thì về danh sách; các mục khác về trang chính phù hợp. Thay trang bằng fallback không được tạo lịch sử giả khiến lần Back tiếp theo rời ứng dụng.
- Expo đặt nút quay lại trong header chung cho Stack và các tab, dùng lịch sử tab khi đã di chuyển. Link mở trực tiếp chưa có lịch sử trở về Tổng quan khi đã đăng nhập hoặc Đăng nhập khi chưa có phiên. Giữ cử chỉ/nút Back hệ thống và các guard quyền hiện có. Kiểm thử trên thiết bị Android/iOS thật vẫn cần cho safe area và tương tác hệ điều hành.
- Web tôn trọng `prefers-reduced-motion`; Expo đọc `AccessibilityInfo` và lắng nghe thay đổi. Chế độ này bỏ dịch chuyển/phóng thu và chuyển cảnh của tab.

## Lựa chọn thư viện

Đã khảo sát tài liệu chính thức ngày 22/10/2026:

| Nguồn | Vai trò trong dự án | Lý do |
|---|---|---|
| [Lucide React](https://lucide.dev/guide/react), [React Native](https://lucide.dev/guide/react-native) | Đã tích hợp, import từng icon | Icon nét thống nhất, giữ nhãn chữ bên cạnh. React Native dùng `react-native-svg` theo phiên bản Expo hỗ trợ. |
| [Motion](https://motion.dev/docs/react-lazy-motion) | Đã tích hợp vào nội dung web | `LazyMotion` + `m`, chỉ bộ tính năng DOM; chính sách [giảm chuyển động](https://motion.dev/docs/react-motion-config). |
| [Radix Dialog](https://www.radix-ui.com/primitives/docs/overview/accessibility) | Đã tích hợp cho menu điện thoại | Có sẵn hành vi bàn phím, quản lý focus, Escape và lớp phủ. |
| [React Bits](https://reactbits.dev/get-started/introduction) | Tham khảo chuyển động, chưa sao chép component | Phân phối dạng mã nguồn theo component, MIT + Commons Clause; chọn hiệu ứng phục vụ tác vụ khi có nhu cầu cụ thể. |
| [Uiverse](https://uiverse.io/) | Tham khảo phản hồi nút/input, chưa sao chép component | UI cộng đồng theo MIT; hiệu ứng cần phù hợp tương phản, cảm ứng và reduced motion của dự án. |

Các hiệu ứng CSS của dự án được viết riêng; không tuyên bố đã tích hợp React Bits/Uiverse. Nếu lấy mã nguồn từ hai nguồn này về sau, lưu giấy phép và ghi nhận tác giả của component. Không cài cả một bộ hiệu ứng chỉ để dùng một nút.

## Kiểm tra khi thay đổi

Kiểm tra desktop và điện thoại: menu, tab hỗ trợ, form đăng nhập, ẩn/hiện mật khẩu, trạng thái chờ, bàn phím, Escape/trả focus, zoom và giảm chuyển động. Chạy lint, typecheck, test và build web; typecheck/test Expo khi đổi giao diện native. Hiệu năng thực tế, đọc màn hình và safe area cần kiểm thử thêm trên thiết bị Android/iOS; kiểm tra tĩnh không thay thế bước này.

Chạy `npm run test:interface --workspace=@sentinellan/company` để kiểm tra giao diện trên trình duyệt với API giả lập, không cần backend. Dừng dev server của cùng ứng dụng trước khi chạy. Bộ kiểm tra tạo ảnh desktop/mobile trong thư mục kết quả của Playwright; ảnh dùng dữ liệu kiểm thử, không phải dữ liệu vận hành.

Kiểm chứng ngày 22/10/2026: lint/typecheck web và Expo đạt; 44 test web, 72 test Expo và 5 luồng giao diện Chromium đạt. Hai cổng web build production thành công; Expo export Android tạo bundle Hermes thành công (chưa phải APK hay kiểm thử trên máy thật). `npm run audit:dependencies` đạt ngưỡng không có High/Critical chưa xử lý sau kiểm chứng backport; còn ba advisory Moderate ở dependency bắc cầu (`decode-uri-component`, `sprintf-js`, `uuid`).
