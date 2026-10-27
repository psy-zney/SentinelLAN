# SentinelLAN mobile

Ứng dụng dùng **Expo SDK 58**, React Native 0.88 và React 19.3. Các module
native phải khớp metadata của Expo; khi nâng SDK, cập nhật cả các override
React Native trong `package.json` ở thư mục gốc.
Hai portal web và `packages/web-ui` cũng dùng React/React DOM 19.3 để tránh
xung đột peer dependency trong npm workspaces.
Root khai báo Expo, React Native và SVG để các config plugin và thư viện icon
được npm đưa lên root vẫn resolve đúng dependency sau khi cài mới.

## Chạy trên Android

Từ thư mục gốc repository, dùng Node.js/npm theo `engines` trong `package.json`:

```powershell
npm.cmd ci
npm.cmd run dev:mobile -- --clear
```

Mở bằng Expo Go hỗ trợ SDK 58 và quét mã QR. Điện thoại và máy phát triển cần
kết nối được với nhau qua mạng LAN. Nếu có thiết bị/emulator kết nối qua ADB:

```powershell
npm.cmd run android --workspace=@sentinellan/mobile -- --clear
```

Sau khi nâng SDK, development build/APK cũ cần được build lại.
Thông báo đẩy trên Android cần development build; Expo Go vẫn mở được ứng dụng
nhưng không đăng ký thông báo đẩy.

Icon dùng SVG gốc tại `packages/web-ui/assets/brand-mark.svg`. Chạy
`node scripts/generate-brand-icons.mjs` từ gốc repository để tạo lại favicon,
Apple touch icon và PNG cho app. Icon launcher, nền adaptive icon và splash
được áp dụng khi build/cài lại ứng dụng; cập nhật JavaScript không thay icon
launcher của bản đã cài.

## Danh sách và chi tiết máy cho Admin

Trong **Tổng quan → Thiết bị công ty**, tìm theo tên máy/hệ điều hành hoặc lọc
trạng thái kết nối. Tìm kiếm và phân trang dùng API, không chỉ lọc trang đang mở.
Chạm vào một máy để xem cấu hình, người được phân công, vị trí, tài sản và số liệu
CPU/RAM/ổ đĩa từ các API `asset-detail` và `telemetry` hiện có.

Tên có tiền tố `[MẪU]` được trình bày gọn kèm nhãn **Dữ liệu mẫu**. Hồ sơ mẫu
ghi rõ chưa có Agent thật kết nối; phần mềm trong hồ sơ là danh sách minh họa,
không phải kết quả kiểm kê. Dữ liệu thiếu được ghi rõ, không tạo thông số thay thế.
Chức năng cấp mã Agent nằm trong mục **Đăng ký Agent mới** có thể mở/thu gọn.

## Theo dõi VPS trên mobile

Đăng nhập bằng tài khoản Admin, mở **Tổng quan → Theo dõi VPS** để xem CPU,
RAM, ổ đĩa, thời gian hoạt động, Docker, dịch vụ hệ thống và cổng đang lắng nghe.
Đây là màn hình chỉ đọc, dùng cùng API `/api/v1/host/status` với dashboard web.
Backend chỉ cho Admin thuộc đơn vị vận hành VPS truy cập; Admin của đơn vị khác
vẫn có thể nhìn thấy nút nhưng API sẽ từ chối và app không hiển thị số liệu.

App làm mới mỗi phút khi màn hình đang mở, dừng kiểm tra và hủy request khi chạy
nền hoặc rời màn hình. Số liệu cũ, thời điểm thu thập lệch, lỗi làm mới, chưa cấu
hình và mất quyền được hiển thị riêng. Dữ liệu VPS không được lưu xuống thiết bị.
Nếu API chưa bật giám sát, cần cấu hình bộ thu thập VPS như dashboard web.

## Cập nhật yêu cầu hỗ trợ

Tab **Yêu cầu** và **Tổng quan → Yêu cầu của tôi** dùng chung dữ liệu hỗ trợ.
Kết quả API tạo yêu cầu được đưa ngay vào cache của trang đầu và trang chi tiết,
kể cả khi ảnh đính kèm gửi thất bại. Thử gửi ảnh lại không tạo thêm yêu cầu.
Lịch sử API sự cố cũ vẫn đọc được qua **Xem báo cáo sự cố trước đây**.

Danh sách, thống kê tổng quan, chi tiết, tin nhắn và ảnh làm mới mỗi 5 giây khi
màn hình đang mở; khi quay lại màn hình hoặc đưa app về trước sẽ tải lại ngay.
Đây là polling gần realtime, cần kết nối API, không phải luồng WebSocket.
Thông báo đẩy nhận được cũng đánh dấu dữ liệu liên quan cần tải lại.
Kéo xuống ở tổng quan hoặc danh sách để làm mới thủ công. Biểu đồ chỉ thống kê
các yêu cầu đã tải (tối đa 100 ở tổng quan, theo từng trang trong danh sách).

## Kiểm tra

```powershell
npm.cmd run check:sdk --workspace=@sentinellan/mobile
npm.cmd run typecheck:mobile
npm.cmd run lint:mobile
npm.cmd run test:mobile -- --runInBand
```

Kiểm thử giao diện dùng React Native Testing Library 14 và Test Renderer;
phải `await` các lệnh render, thao tác giao diện và unmount.

`decode-uri-component` ở devDependencies của root là dependency cho bộ test
hồi quy bảo mật cũ. Expo Router 58 không còn dùng package này; postinstall vẫn
áp dụng và xác minh backport trước khi chạy bộ test bảo mật.
