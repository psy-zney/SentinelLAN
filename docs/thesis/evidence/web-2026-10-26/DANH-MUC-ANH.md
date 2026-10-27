# Bộ ảnh nghiệm thu web SentinelLAN — 26/10/2026

41 ảnh PNG gốc chụp trực tiếp website production qua Chromium, hai phiên riêng cho Admin/Employee. Không dựng giao diện, không sửa dữ liệu để giả kết quả và không chèn mật khẩu/token/cookie vào bộ bàn giao. Mobile chưa được thực hiện theo yêu cầu người dùng.

## Thao tác và kết quả thực tế

- Đăng nhập hai vai trò thành công. Thông tin đăng nhập không được lưu trong gói ảnh.
- Chuyển phân công nhân viên được chỉ định từ máy ZNEY đang offline sang SWEETYCANDY. Hai thiết bị và credential được giữ nguyên; không thu hồi hoặc xóa thiết bị. Giao máy mới được xác minh lại sau khi reload và tại cổng Employee.
- Xác minh SWEETYCANDY Online, Agent 1.0.4. Hai lượt quan sát cuối có thời điểm kết nối 15:51:56 và 15:53:32 ngày 26/10/2026 (UTC+07:00); dữ liệu chi tiết ở `evidence/heartbeat-observations.json`.
- Tạo đúng một yêu cầu `NGHIỆM THU ĐỒ ÁN — Hỗ trợ máy SWEETYCANDY`, ID `050054ae-b3c0-48b1-8dcd-121be10f9d3e`. Nội dung ghi rõ đây là nghiệm thu, không phải tuyên bố máy đang hỏng. Yêu cầu hiện ở trạng thái **IT đang xử lý**, được phân công Organization Administrator; chưa đánh dấu đã giải quyết.
- Nhân viên chọn ảnh chụp giám sát được phép, xem trước, xác nhận đồng ý rồi gửi; Admin mở được ảnh đính kèm của đúng yêu cầu.
- Gửi một lệnh an toàn `CollectTelemetryNow`, ID `050ede9a-91e6-4a67-a46a-9162a226a2db`. Có biên lai **Succeeded** với nội dung `Fresh system telemetry collected and acknowledged by the server` và audit CommandCompleted. Không gửi lệnh khóa/cô lập/gỡ Agent hoặc thay đổi policy.
- Đọc giám sát VPS, Docker, image API, topology, CPU/RAM/disk, cổng đang nghe; không sửa dịch vụ VPS.
- Image API quan sát trên web: `sentinellan-api:git-af4fb1d3860f7623f49529c5f6ce01d0c9334350`. Kết quả public health/compatibility lưu trong `evidence/public-system-status.json`.
- Đã đọc audit, chính sách, cảnh báo và tài khoản. Không thay đổi trạng thái cảnh báo, quyền tài khoản hoặc chính sách.

## Cách dùng cho đồ án

Mở `index.html` để xem ảnh và chú thích, hoặc dùng từng PNG trong `screenshots/`. `manifest.json` có URL trang, vai trò, thời điểm UTC/UTC+07 và SHA-256 từng ảnh. Ảnh không chứa browser chrome; URL nguồn được ghi trong metadata.

Nhóm ảnh nên ưu tiên đưa vào bài: W02, W03, W04, W07–W10, W12–W15, W19–W22, W25, W27, W32, W35 và W36/W40. Các ảnh toàn trang dài là bằng chứng bổ sung; khi chèn Word nên ưu tiên khung hình có chữ đọc được. Chưa sửa DOCX gốc hoặc tự đổi số hình.

- W05: form lần thử phân công ban đầu. Backend từ chối 409 do nhân viên còn giữ máy ZNEY; sau đó đã chuyển phân công theo đúng luồng UI. Không dùng W05 như bằng chứng lưu thành công.
- W06: trạng thái trước bàn giao, hiển thị ZNEY offline; không phải kết quả sau khi giao SWEETYCANDY.
- W33: sự kiện lúc phát lệnh có trạng thái Đang chờ. Đây là lịch sử sự kiện, không phải trạng thái cuối của lệnh; xem W22/W32 cho biên lai thành công.
- W34: cấu hình bảo vệ mẫu được UI đánh dấu; không có nghiệm thu áp policy lên Windows trong phiên này.
- W35: cảnh báo chống mã độc/mã hóa ổ đĩa cần IT kiểm tra được giữ nguyên. Không suy ra máy đã an toàn hoặc đã bị nhiễm từ nhãn cảnh báo.
- W27: sơ đồ dịch vụ do màn giám sát dựng theo cấu hình/snapshot; không thay thế đo TLS hay kiểm thử kết nối độc lập trên mọi nhánh.

## Điểm UI cần rà soát riêng

- Xung đột phân công 409 hiện được diễn đạt chung là lỗi thao tác/kết nối, chưa giải thích ngay việc nhân viên đã giữ một máy.
- Phần cấu hình chi tiết có chỗ hiện `ManualSpecifications: null` và `Inventory: [object Object]`.
- Sau khi tạo yêu cầu ở Hỗ trợ IT, mục Yêu cầu đã gửi trên trang Máy tính của tôi vẫn hiển thị chưa có sự cố; yêu cầu mới vẫn có đầy đủ ở Hỗ trợ IT của cả hai vai trò.

Các quan sát trên không được sửa hoặc che bằng cách đổi DOM/ảnh. Ảnh và kết quả chỉ chứng minh những bước đã thực hiện, không thay thế kiểm thử bảo mật độc lập, phép đo hiệu năng, nghiệm thu mobile hay hành động OS khác.

## Phần còn lại

Ảnh cài Setup, service Windows Running và ảnh native mà người dùng đã gửi trước không được giả lập lại bằng web. Mobile sẽ do người dùng bổ sung sau. Bộ ảnh này không tự chứng minh camera/push native, reboot, mất mạng vật lý hoặc khôi phục mạng.

## Danh mục ảnh

| Mã | Nội dung | Vai trò | Thời điểm UTC+07 |
| --- | --- | --- | --- |
| W00-admin-login | Trang đăng nhập quản trị | Admin | 2026-10-26T15:19:33.849000+07:00 |
| W01-employee-login | Trang đăng nhập nhân viên | Employee | 2026-10-26T15:20:53.689000+07:00 |
| W02-admin-dashboard | Tổng quan quản trị trên hệ thống production | Admin | 2026-10-26T15:23:17.670000+07:00 |
| W03-admin-device-list | Thiết bị SWEETYCANDY đang kết nối trước phân công | Admin | 2026-10-26T15:23:49.622000+07:00 |
| W04-admin-device-hardware | Thông tin phần cứng và bảo vệ thiết bị SWEETYCANDY | Admin | 2026-10-26T15:25:28.457000+07:00 |
| W05-assign-device-confirmation | Xác nhận giao SWEETYCANDY cho nhân viên | Admin | 2026-10-26T15:26:39.441000+07:00 |
| W06-employee-before-handover | Máy ZNEY đang được giao trước khi chuyển phân công | Employee | 2026-10-26T15:27:50.491000+07:00 |
| W07-assign-sweetycandy | Form giao máy SWEETYCANDY cho Employee | Admin | 2026-10-26T15:28:21.519000+07:00 |
| W08-admin-assigned-device | SWEETYCANDY đã được giao cho nhân viên và đang Online | Admin | 2026-10-26T15:28:53.176000+07:00 |
| W09-admin-device-online | Trạng thái Online, CPU/RAM và người sử dụng của SWEETYCANDY | Admin | 2026-10-26T15:28:53.311000+07:00 |
| W10-employee-assigned-device | Employee xem máy SWEETYCANDY vừa được giao | Employee | 2026-10-26T15:28:55.721000+07:00 |
| W11-employee-support-home | Cổng hỗ trợ IT dành cho nhân viên | Employee | 2026-10-26T15:30:05.639000+07:00 |
| W12-employee-create-request | Nhân viên mô tả yêu cầu hỗ trợ nghiệm thu trên máy được giao | Employee | 2026-10-26T15:31:02.890000+07:00 |
| W13-employee-request-created | Yêu cầu hỗ trợ đã tạo thành công trên SWEETYCANDY | Employee | 2026-10-26T15:31:15.284000+07:00 |
| W14-employee-image-preview | Xem trước ảnh minh chứng và yêu cầu đồng ý trước khi gửi | Employee | 2026-10-26T15:32:26.911000+07:00 |
| W15-employee-image-consent | Nhân viên xác nhận đồng ý gửi ảnh đã chọn | Employee | 2026-10-26T15:32:26.997000+07:00 |
| W16-employee-after-image-upload | Yêu cầu hỗ trợ sau khi gửi ảnh minh chứng | Employee | 2026-10-26T15:32:44.599000+07:00 |
| W17-admin-support-queue | Admin tiếp nhận yêu cầu hỗ trợ mới từ Employee | Admin | 2026-10-26T15:32:48.041000+07:00 |
| W18-admin-request-triage | Admin phân công và tiếp nhận xử lý yêu cầu nghiệm thu | Admin | 2026-10-26T15:34:26.340000+07:00 |
| W19-admin-request-in-progress | Yêu cầu nghiệm thu được Admin tiếp nhận và phân công xử lý | Admin | 2026-10-26T15:34:57.922000+07:00 |
| W20-employee-request-progress | Employee thấy trạng thái và người phụ trách sau khi IT tiếp nhận | Employee | 2026-10-26T15:35:00.212000+07:00 |
| W21-safe-telemetry-command | Xác nhận lệnh cập nhật thông số máy SWEETYCANDY | Admin | 2026-10-26T15:38:58.670000+07:00 |
| W22-command-success-receipt | Lệnh CollectTelemetryNow thành công và có biên lai Agent | Admin | 2026-10-26T15:41:28.407000+07:00 |
| W23-vps-overview | Giám sát VPS Oracle qua giao diện quản trị | Admin | 2026-10-26T15:38:59.701000+07:00 |
| W24-vps-docker-sentinellan | Bảy container SentinelLAN trong màn hình giám sát Docker | Admin | 2026-10-26T15:42:39.566000+07:00 |
| W25-vps-api-image | Image API trên VPS khớp commit release af4fb1d | Admin | 2026-10-26T15:43:55.578000+07:00 |
| W26-vps-services | Danh sách dịch vụ trên VPS trong chế độ giám sát | Admin | 2026-10-26T15:43:59.825000+07:00 |
| W27-vps-sentinellan-topology | Đường đi API, web và PostgreSQL trong nhánh SentinelLAN | Admin | 2026-10-26T15:45:32.486000+07:00 |
| W28-vps-cpu | Chi tiết CPU VPS và các container đang chạy | Admin | 2026-10-26T15:47:09.446000+07:00 |
| W29-vps-memory | Chi tiết bộ nhớ VPS và mức dùng theo container | Admin | 2026-10-26T15:47:12.672000+07:00 |
| W30-vps-disk | Giám sát dung lượng theo thành phần và dịch vụ trên VPS | Admin | 2026-10-26T15:48:34.452000+07:00 |
| W31-vps-listening-ports | Danh sách cổng đang nghe trên VPS ở chế độ chỉ đọc | Admin | 2026-10-26T15:48:37.676000+07:00 |
| W32-audit-operations | Audit ghi nhận phân công thiết bị, hỗ trợ và lệnh an toàn | Admin | 2026-10-26T15:48:51.748000+07:00 |
| W33-audit-command-issued | Audit ghi nhận thời điểm ban hành lệnh có lý do | Admin | 2026-10-26T15:49:56.942000+07:00 |
| W34-protection-policies | Danh sách cấu hình bảo vệ và giới hạn áp dụng hiển thị trên web | Admin | 2026-10-26T15:49:57.014000+07:00 |
| W35-device-alerts | Cảnh báo thực tế của SWEETYCANDY và các tình huống mẫu được đánh dấu | Admin | 2026-10-26T15:51:59.999000+07:00 |
| W36-employee-live-heartbeat | Employee quan sát heartbeat mới và phiên bản Agent 1.0.4 | Employee | 2026-10-26T15:52:02.757000+07:00 |
| W37-device-use-history | Lịch sử sử dụng hiển thị của thiết bị SWEETYCANDY | Admin | 2026-10-26T15:53:33.087000+07:00 |
| W38-admin-received-image | Admin mở được ảnh đính kèm của yêu cầu hỗ trợ | Admin | 2026-10-26T15:53:36.299000+07:00 |
| W39-internal-accounts | Vai trò và trạng thái tài khoản nội bộ Admin và Employee | Admin | 2026-10-26T15:52:00.074000+07:00 |
| W40-employee-heartbeat-next | Heartbeat mới ở lượt quan sát kế tiếp của Employee | Employee | 2026-10-26T15:53:38.487000+07:00 |
