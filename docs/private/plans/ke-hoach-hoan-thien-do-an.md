# Kế hoạch hoàn thiện đồ án SentinelLAN

Ngày lập: 04/10/2026. Trạng thái: kế hoạch triển khai và nghiệm thu, chưa phải báo cáo tính năng đã hoàn thành.

## 1. Mục tiêu và phạm vi đã chốt

Xây dựng hệ thống để một công ty tự quản lý máy tính Windows trong mạng nội bộ: biết máy nào đang hoạt động, ai đang sử dụng, tiếp nhận yêu cầu hỗ trợ, xử lý bằng các thao tác được cho phép và lưu được lịch sử. Nhân viên văn phòng có thể sử dụng mà không nhớ lệnh hoặc hiểu thuật ngữ kỹ thuật.

- Người thực hiện: một người; thời gian không phải ràng buộc chính. Hoàn thành theo cổng nghiệm thu thay vì lịch tuần cố định.
- Thiết bị: hai laptop Windows, có thể mượn laptop thứ ba; hai VPS. Windows là nền tảng duy nhất phải hoàn thiện trong phiên bản này.
- Bản chính: một công ty tự vận hành một hệ thống; không cần tài khoản Super Admin/Platform Owner của nhà cung cấp.
- Vai trò: Quản trị công ty (Admin), Kỹ thuật viên (Technician), Nhân viên (Employee). Agent là danh tính máy, không phải tài khoản nhân viên.
- Truy cập: Ethernet hoặc Wi-Fi có đường mạng đến máy chủ; cùng router chưa đảm bảo kết nối nếu có guest isolation/VLAN/firewall. Máy chủ nội bộ có địa chỉ ổn định và tên DNS cùng chứng chỉ TLS được thiết bị tin cậy.
- Giao diện web cho IT và nhân viên; Agent Windows có bộ cài và phần giao diện tương tác cần thiết. Không bắt buộc ứng dụng điện thoại.
- macOS, SaaS nhiều công ty, cổng Platform Owner, quản trị VPS và chức năng điều khiển máy tùy ý nằm ngoài nghiệm thu chính. Giữ mã hiện có riêng biệt, không xóa để thu hẹp phạm vi.
- Không thêm theo dõi bí mật, chụp màn hình tự động, thu thập mật khẩu hoặc chạy lệnh shell tùy ý. Ảnh hỗ trợ do nhân viên tự chọn và đồng ý gửi.

Thành công phải được chứng minh bằng một chuỗi hoạt động thực tế từ cài đặt đến hỗ trợ và thu hồi máy, chứ không chỉ bằng số màn hình hoặc số test.

## 2. Điểm xuất phát và khoảng trống

Những nhận định dưới đây dựa trên mã và tài liệu đã rà soát; trạng thái chạy thực tế phải được xác nhận lại ở từng cổng nghiệm thu.

| Phần | Đã có trong mã/kiểm tra trước đó | Cần hoàn thiện hoặc chứng minh |
|---|---|---|
| Tài khoản, phân quyền | Phiên đăng nhập, vai trò, kiểm tra tenant, thu hồi phiên qua SecurityStamp | Ma trận quyền toàn bộ API, khôi phục tài khoản, bootstrap công ty độc lập |
| Triển khai | Compose, reverse proxy, PostgreSQL, các cổng web | Profile self-host không yêu cầu Platform Owner; kiểm thử mất Internet và khôi phục backup |
| Kết nối Agent | Setup.exe/MSI, nhập một mã, phản hồi lỗi, chờ hoạt động thành công | Cài trên Windows sạch; chuyển thiết kế SL1 sang token ngẫu nhiên với bộ cài theo công ty |
| Token đăng ký | Token ngẫu nhiên, hash, hạn dùng, UsedAt có concurrency control | Tranh chấp đăng ký trên PostgreSQL thật; xử lý mất phản hồi sau khi token đã dùng |
| Giám sát | CPU/RAM/ổ hệ thống, OS, phiên bản Agent, heartbeat, hàng đợi cục bộ có giới hạn | Độ chính xác, thiếu dữ liệu, mất mạng, khởi động lại và tiêu thụ tài nguyên trên laptop thật |
| Hỗ trợ | Yêu cầu, hội thoại, ảnh, lịch hẹn, thông báo, phê duyệt | Chuẩn hóa vòng đời; thống nhất với incident/work order, tránh hai luồng báo lỗi trùng nhau |
| Lệnh và chính sách | Danh mục thao tác, receipt, nonce, mô phỏng và adapter Windows | Thay khóa HMAC dùng chung bằng chữ ký bất đối xứng; phân biệt duyệt/gửi/thực thi/xác minh |
| Cảnh báo | Tạo, xác nhận, xử lý; đã có một số xử lý mất kết nối | Quy tắc ngưỡng tài nguyên có thời gian duy trì, chống lặp và cơ chế hết cảnh báo |
| Tài sản | Hồ sơ, lịch sử, bàn giao/cho mượn, công việc bảo trì | Tính nhất quán khi đổi người dùng, cài lại Agent, thu hồi và thanh lý |
| Tài liệu | Nhiều ADR và tài liệu nghiệp vụ | Một số mô tả chưa khớp mã; phải cập nhật theo bằng chứng đã nghiệm thu |

Lần kiểm tra trước đã ghi nhận 270 test .NET và 34 test web đạt, cùng một số kiểm tra build/lint/typecheck. Đây không phải kết quả chạy lại cho kế hoạch này. Integration dùng InMemory không thay thế kiểm thử PostgreSQL; bộ cài chưa được nghiệm thu đầy đủ trên Windows sạch. Không dùng những con số này để tuyên bố hệ thống đã sẵn sàng triển khai.

## 3. Kiến trúc đích và môi trường thử nghiệm

Giữ kiến trúc modular monolith ASP.NET Core, PostgreSQL, Next.js/TypeScript và Agent C# Windows hiện tại. Giữ phiên bản dependency đã ghim và kiểm tra; nâng cấp khi có lý do tương thích hoặc bảo mật. Không thêm microservices, Kubernetes, Kafka hoặc AI để tăng số công nghệ.

```text
Nhân viên / IT -- HTTPS --> Web + API -- mạng riêng --> PostgreSQL
Agent Windows -- HTTPS --> API
Desktop companion <---- IPC có kiểm soát ----> Windows service
API -- backup được mã hóa --> nơi lưu tách biệt theo cấu hình của công ty
```

Agent chủ động kết nối ra API, không cần mở cổng điều khiển vào từng laptop. SignalR giúp cập nhật giao diện; dữ liệu và trạng thái phải lấy lại được qua API khi kết nối SignalR bị mất.

Ranh giới mã: Domain chứa quy tắc; Application chứa use case và quyền; Infrastructure chứa EF/HTTP/OS; API là transport và composition root. Agent Core không phụ thuộc Windows; adapter Windows nằm trong Infrastructure. Các module trao đổi qua contract/event, không đọc trực tiếp bảng của nhau.

| Môi trường | Cách bố trí đề xuất | Mục đích |
|---|---|---|
| Phát triển | Laptop 1: mã nguồn, build, database dev | Lập trình và test nhanh |
| Lab LAN chính | Laptop 1 chạy server trong VM/Compose nếu đủ tài nguyên; laptop 2 chạy Agent thật | Chứng minh công ty tự quản lý, Wi-Fi/Ethernet, mất Internet |
| Windows sạch | VM có snapshot hoặc laptop mượn | Cài mới, nâng cấp, gỡ cài đặt; không phá môi trường học/làm việc chính |
| VPS 1 | Staging với dữ liệu giả, TLS, truy cập quản trị hạn chế | Kiểm thử đóng gói và triển khai từ đầu, cập nhật phiên bản |
| VPS 2 | Môi trường khôi phục và bản sao backup có quyền riêng | Chứng minh phục hồi sang máy chủ khác |

Không coi hai VPS là hệ thống HA đã hoàn thiện. Bản LAN phải hoạt động khi Internet mất nhưng router/mạng nội bộ còn hoạt động. Triển khai server trên VPS là một phương án riêng cần đường Internet/VPN, không được dùng để chứng minh tính độc lập Internet của bản LAN. Cần kiểm tra RAM, dung lượng, khả năng VM và giấy phép hệ điều hành trước khi chốt sơ đồ lab.

## 4. Ma trận quyền tối thiểu

| Thao tác | Admin | Technician | Employee |
|---|---|---|---|
| Tài khoản, vai trò, cấu hình công ty | Có | Không | Hồ sơ cá nhân theo phạm vi |
| Cấp/thu hồi token đăng ký | Có | Chỉ khi bổ sung quyền ủy nhiệm rõ ràng | Không |
| Bàn giao/thu hồi thiết bị | Có | Theo quyền ủy nhiệm nếu được triển khai | Xem máy đang được giao |
| Xem thiết bị | Toàn công ty | Toàn công ty ở phạm vi hiện tại | Máy của mình |
| Tiếp nhận hỗ trợ | Có | Có; có người chịu trách nhiệm từng yêu cầu | Tạo và trao đổi yêu cầu của mình |
| Thao tác từ xa | Theo danh mục và chính sách | Theo danh mục và chính sách | Chỉ hành động tự phục vụ được phép |
| Duyệt thao tác nhạy cảm | Theo chính sách | Không tự mở rộng quyền từ việc được giao ticket | Xác nhận trên máy khi luồng yêu cầu |
| Audit quản trị | Có | Chỉ lịch sử công việc được phép | Tiến độ yêu cầu của mình |

Quyền phải được kiểm tra ở API cho mọi request, kể cả khi nút đã ẩn trên giao diện. Một công ty không phải lý do bỏ tenant boundary. Áp dụng mặc định từ chối và kiểm tra quyền trên từng request theo [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html).

## 5. Danh mục luồng phải hoàn thiện

P0 là điều kiện để nhận bản chính; P1 là chiều sâu ứng dụng cần hoàn thiện tiếp; P2 là mở rộng sau khi bản chính ổn định. Thứ tự làm theo giai đoạn ở mục 10, không triển khai đồng thời toàn bộ.

| ID / mức | Luồng và người thực hiện | Kết quả phải đạt | Ngoại lệ bắt buộc |
|---|---|---|---|
| F01 P0 | Admin triển khai công ty lần đầu | Khởi tạo một lần, tạo Admin, kiểm tra DB/TLS/backup; không cần Platform Owner | Thiếu cấu hình, DB chưa sẵn sàng, chạy bootstrap lại, sai chứng chỉ |
| F02 P0 | Đăng nhập, đăng xuất, phiên hết hạn | Phân biệt đúng vai trò; thông báo dễ hiểu; khóa tài khoản thu hồi phiên | Sai mật khẩu, giới hạn thử, refresh đồng thời, session cũ, bảo vệ CSRF với cookie |
| F03 P0 | Admin quản lý người dùng | Mời/kích hoạt, đổi quyền, khóa, khôi phục; giữ ít nhất một Admin hoạt động | Mời hết hạn/đã dùng, người nghỉ việc, mất tài khoản Admin cuối cùng |
| F04 P0 | Admin cấp mã kết nối | Mã ngẫu nhiên, hết hạn, thu hồi, dùng đúng một lần; xem trạng thái không lộ lại secret | Hai máy dùng đồng thời; mã hết hạn/đã dùng; nhập sai, đoán mã |
| F05 P0 | IT/nhân viên cài Agent | Mở bộ cài, nhập mã, thấy từng bước; hoàn tất khi máy thật sự gửi dữ liệu | Thiếu quyền, sai server/TLS, mất mạng, MSI lỗi, đã cài, reboot, mất phản hồi đăng ký |
| F06 P0 | IT bàn giao và nhận lại máy | Máy gắn đúng người; lịch sử không mất; nhân viên chỉ thấy máy đang được giao | Chuyển nhân sự, hai người nhận cùng lúc, máy đang có ticket/loan mở |
| F07 P0 | Agent hoạt động thường ngày | Service tự khởi động, bảo vệ danh tính, heartbeat, telemetry, retry có giới hạn | Sleep/wake, đổi Wi-Fi, offline, đầy đĩa, đồng hồ lệch, server tạm lỗi |
| F08 P0 | IT xem tổng quan và chi tiết máy | Số máy và trạng thái nhất quán; thời điểm nhận dữ liệu rõ; tìm/lọc/phân trang | Chưa có máy, dữ liệu cũ, thiếu metric, API lỗi, SignalR mất kết nối |
| F09 P0 | Nhân viên báo sự cố | Chọn triệu chứng và “Bạn có tiếp tục làm việc được không?”; tự chọn máy hợp lệ | Chưa có máy, nhiều máy, gửi lặp, ảnh sai định dạng/quá lớn, offline |
| F10 P0 | IT tiếp nhận và xử lý yêu cầu | Có người phụ trách, trao đổi, hẹn, chờ phản hồi, giải quyết, đóng/mở lại | Hai IT nhận cùng lúc, IT vắng, nhân viên đổi máy, yêu cầu bị từ chối |
| F11 P0 | Nhân viên gửi ảnh hỗ trợ | Tự chọn ảnh, xem trước, xóa trước khi gửi; chỉ người có quyền xem | Upload giả loại file, truy cập ảnh người khác, file bị xóa/hết thời gian lưu |
| F12 P0 | IT gửi thao tác an toàn | Có lý do, quyền, hạn, nonce, audit; trạng thái thực thi và xác minh riêng | Replay, chữ ký sai, hết hạn, sai thiết bị, mất mạng, kết quả không xác định |
| F13 P0 | Theo dõi và xử lý cảnh báo | Online/offline và một bộ quy tắc tài nguyên nhỏ; nhận xử lý và kết thúc | Flapping, heartbeat trễ, bảo trì, cảnh báo lặp, dữ liệu không đủ |
| F14 P0 | Xin cài phần mềm được duyệt | Chọn từ catalog; duyệt; cài hoặc IT xử lý thủ công có ghi nhận; xác minh kết quả | Hash/publisher sai, hết dung lượng, cần reboot, thiếu đặc quyền, cài thất bại |
| F15 P0 | Xin bảo trì/tạm dừng hoặc gỡ Agent | Phê duyệt và OTP có phạm vi; xác nhận tại máy; bảo trì tự hết hạn | OTP sai/hết hạn/đã dùng, Agent offline, người không đúng, thiếu quyền gỡ |
| F16 P0 | Thu hồi máy hoặc Agent | Credential bị thu hồi; lần kết nối tiếp theo bị từ chối; giữ lịch sử | Máy offline lúc thu hồi, cài lại, đổi người sử dụng, thu hồi nhầm |
| F17 P0 | Sao lưu và phục hồi | Backup DB, cấu hình, attachment và khóa cần thiết; phục hồi kiểm tra được | Backup hỏng, mất khóa, khác phiên bản schema, máy chủ cũ còn chạy |
| F18 P0 | Nâng cấp hệ thống và Agent | Version rõ, backup trước migration, kiểm tra tương thích và kế hoạch quay lui | Nâng cấp dở, phiên bản Agent cũ, migration lỗi, reboot trong cài đặt |
| F19 P1 | IT quản lý chính sách | Tạo phiên bản, gán, áp dụng thử, xem kết quả; chỉ chính sách hỗ trợ | Chính sách xung đột, Agent không hỗ trợ, rollback, máy offline |
| F20 P1 | Quản lý tài sản và cho mượn | Hồ sơ, bảo trì, bàn giao, trả; đối chiếu người/máy và lịch sử | Trả trễ, mất/hỏng, cho mượn trùng, thanh lý khi còn việc mở |
| F21 P1 | Lịch hẹn, thông báo, hướng dẫn | Đặt/đổi/hủy lịch; thông báo sự cố chung; nhân viên xác nhận đã đọc | Trùng lịch, quá hạn, thông báo hết hiệu lực, không có kết nối realtime |
| F22 P1 | QR trên thiết bị | Quét để xác định thiết bị; thông tin chi tiết yêu cầu đăng nhập | Mã giả, mã thu hồi, quyền sai, máy thanh lý; không lộ thông tin nội bộ công khai |
| F23 P1 | Báo cáo và xuất dữ liệu | Tồn kho, thời gian xử lý, sự cố lặp, tình trạng thiết bị; có phạm vi ngày | Dữ liệu rỗng, múi giờ, file lớn, phân quyền export, CSV formula injection |
| F24 P1 | Báo sự cố nghiêm trọng/Panic | Chuyển cảnh báo ưu tiên cho IT, hiển thị hướng dẫn và tiến độ | Không hứa tự cô lập nếu chế độ thật chưa được cấp quyền; chống gửi dồn |

Trong F14, “xin quyền” chỉ là xin thực hiện thao tác/phần mềm được duyệt. Không cấp quyền quản trị Windows chung cho nhân viên. Tính năng khóa/cô lập thật chỉ chạy với lab flag và thiết bị thử nghiệm đã cho phép; bản mặc định tiếp tục giữ chế độ an toàn.

## 6. Chốt vòng đời và tính nhất quán

### 6.1 Thiết bị và đăng ký

Tách ba đối tượng: tài sản vật lý, lần cài Agent, và việc giao tài sản cho nhân viên. Cài lại Windows không được làm mất lịch sử tài sản; cũng không được nhận diện tin cậy chỉ bằng hostname/MAC. IT đối chiếu và xác nhận liên kết khi thay Agent.

Luồng chính: tạo token → cài Agent → đăng ký thành công → cấp credential riêng → heartbeat đầu tiên → IT nhận diện/bàn giao → vận hành → thu hồi credential → gỡ/luân chuyển/thanh lý theo nhu cầu.

Online/offline là trạng thái quan sát, không đồng nghĩa với đã thu hồi. Máy có thể “đã thu hồi” nhưng vẫn gửi request bị từ chối; máy “đang sử dụng” có thể offline. Lưu riêng lifecycle, kết nối và trạng thái bảo trì.

### 6.2 Hỗ trợ

Chọn SelfService Request làm cửa vào duy nhất của nhân viên. Incident/work order là hồ sơ xử lý liên quan phía IT, có liên kết đến request; không tạo hai yêu cầu độc lập cho cùng một thao tác gửi.

Trạng thái ticket chính: Mới → Đang xử lý → Chờ nhân viên (nếu cần) → Đã giải quyết → Đã đóng. Nhánh phê duyệt có trạng thái riêng Chờ duyệt/Đã duyệt/Từ chối. Chốt bảng chuyển trạng thái theo loại yêu cầu; chỉ loại phù hợp mới cần duyệt. Mở lại phải có lý do và giữ lịch sử.

Trạng thái hành động kỹ thuật tách khỏi ticket: Chờ gửi → Agent nhận → Đang thực hiện → Thành công/Thất bại/Hết hạn/Không xác định. “Admin đã duyệt” không đồng nghĩa phần mềm đã cài. Nhân viên nhận thông báo theo kết quả thực tế.

### 6.3 Đồng thời và gửi lại

Dùng transaction/concurrency control cho nhận ticket, nhận máy, trả tài sản, dùng token/OTP. Các request có thể gửi lặp cần idempotency key và kiểm tra payload tương ứng. Ghi audit cùng giao dịch thay đổi dữ liệu quan trọng; tác vụ bất đồng bộ phải có cơ chế phát lại bền vững khi cần.

Không tuyên bố thực thi OS “đúng một lần” khi Agent có thể crash sau khi thao tác nhưng trước receipt. Với kết quả chưa biết, kiểm tra trạng thái thực tế hoặc chuyển IT xác minh; không tự gửi lại thao tác có tác động chỉ vì timeout.

## 7. Hoàn thiện bảo mật theo thiết kế

### 7.1 Mã kết nối một lần

Mã SL1 hiện tại biểu diễn dữ liệu bằng Base64URL, không mã hóa bảo mật, nên có thể đọc nội dung kể cả sau khi hết hạn. Hết hạn chỉ ngăn sử dụng nếu server thực thi kiểm tra; không xóa thông tin đã chứa trong mã.

Thiết kế đích đề xuất cho bản công ty tự quản lý:

1. IT tạo/tải bộ cài dành cho công ty, chứa địa chỉ HTTPS đã cấu hình và thông tin tin cậy công khai cần thiết. Không chứa mật khẩu Admin, khóa ký riêng hoặc secret dùng chung để giải mã.
2. Admin cấp token ngẫu nhiên mật mã 256 bit. Nhân viên chỉ dán token; không nhớ URL hay lệnh. Token không chứa IP, user, cấu hình hoặc quyền có thể giải mã ra.
3. Server chỉ lưu hash token, công ty, người cấp, thời hạn, trạng thái dùng/thu hồi và ràng buộc bổ sung nếu có. Địa chỉ server không phải bí mật: vẫn có thể biết qua bộ cài và lưu lượng mạng.
4. Server kiểm tra và tiêu thụ token nguyên tử cùng việc tạo danh tính Agent. Hai request cạnh tranh chỉ một request được nhận đăng ký.
5. Sau đăng ký, dùng credential riêng có khả năng thu hồi/luân chuyển; xóa token khỏi cấu hình tạm. Không đưa token vào log, URL, tham số dòng lệnh hoặc MSI property có thể bị ghi log.
6. Nếu server đã nhận nhưng phản hồi mất, không mở lại token. Thiết kế phục hồi ràng buộc bằng bằng chứng danh tính đã tạo trên máy; nếu chưa có cơ chế an toàn, IT thu hồi đăng ký dở và cấp mã mới.
7. Giới hạn thử theo nguồn và tổng tải; phản hồi lỗi không tiết lộ dữ liệu nhạy cảm. Không log toàn bộ request đăng ký.

Các nguyên tắc ngẫu nhiên mật mã, dùng một lần, hạn ngắn và lưu trữ an toàn được áp dụng tương tự hướng dẫn token trong [OWASP Forgot Password Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html); đây là token đăng ký thiết bị, không phải luồng đặt lại mật khẩu.

Đầu ra thiết kế cần có cả quy trình dựng/ký/cập nhật bộ cài theo công ty. Không thay nội dung cấu hình sau khi ký file mà vẫn tuyên bố chữ ký còn hợp lệ. Lab chưa có chứng chỉ ký code tin cậy phải ghi rõ giới hạn, không hướng dẫn tắt kiểm tra TLS hoặc bảo vệ Windows để che lỗi.

### 7.2 Lệnh và đặc quyền Windows

- Thay HMAC dùng chung giữa server và mọi Agent bằng chữ ký bất đối xứng: server giữ khóa riêng, Agent giữ khóa công khai. Chữ ký xác minh nguồn và tính toàn vẹn, không che nội dung; xem [Microsoft: Cryptographic signatures](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptographic-signatures).
- Envelope ký bao gồm phiên bản định dạng, command ID, device ID, loại thao tác, tham số chuẩn hóa, issuedAt, expiresAt, nonce và key ID. Quy định rõ serialization; test sửa từng trường, replay và sai máy.
- Thiết kế bootstrap tin cậy và luân chuyển khóa trước khi bỏ HMAC; bản cuối không giữ đường fallback yếu âm thầm. Không tải khóa mới từ nguồn chưa được xác thực.
- Service giữ quyền tối thiểu. Desktop companion chỉ xử lý tương tác của đúng phiên người dùng; IPC kiểm tra ACL, danh tính và schema.
- P0 cho cài phần mềm/gỡ Agent: hỗ trợ IT thực hiện có nâng quyền tại máy và ghi nhận kết quả. P1 có thể bổ sung broker đặc quyền hẹp với catalog/operation cố định. Không nâng toàn bộ Agent lên Administrator chỉ để làm cho demo chạy được.
- MSI ứng dụng phải khớp SHA256 và Authenticode publisher trong catalog đã duyệt; snapshot thông tin tại lúc duyệt, không đổi gói âm thầm sau phê duyệt. Thiếu cơ sở xác minh thì không cài.
- PauseAgent là bảo trì có hạn, vẫn giữ kênh sức khỏe/điều khiển cần thiết, tự trở lại hoạt động. UI diễn đạt đúng, không báo “đã tắt Agent” nếu thực tế chưa tắt.

### 7.3 Web, dữ liệu và vận hành

- Kiểm tra quyền, tenant, đối tượng và chuyển trạng thái ở server. Không tin trạng thái do giao diện gửi lên; tham khảo [OWASP REST Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html).
- Chốt session cookie, CSRF, CORS, rate limit, recovery và thu hồi phiên. MFA Admin là P1; recovery P0 phải có quy trình rõ ràng, không phụ thuộc email Internet cho bản LAN.
- Attachment riêng tư, xác thực nội dung, giới hạn kích thước, kiểm tra quyền khi tải; file mồ côi được dọn. Không dùng URL public làm đường tắt.
- Audit ghi người, đối tượng, lý do, thời gian, kết quả và correlation ID; không ghi secret. Chặn sửa qua ứng dụng chưa chứng minh chống DBA sửa trực tiếp: ghi đúng giới hạn và cân nhắc DB role chỉ append/export sang kho riêng.
- Chính sách lưu dữ liệu đề xuất để thử nghiệm: telemetry thô 30 ngày, yêu cầu/ảnh 90 ngày sau đóng, audit 180 ngày; cho Admin cấu hình và kiểm tra cleanup. Đây là mặc định đồ án đề xuất, không phải thời hạn pháp luật.
- Backup mã hóa, phân quyền riêng; lưu và phục hồi khóa một cách có kiểm soát. Không commit secret, dữ liệu thật, chứng chỉ riêng hoặc dump.
- Kiểm tra dependency và bản phát hành theo phiên bản thực dùng; lưu kết quả và ngoại lệ. Công cụ không truy cập được nguồn advisory phải ghi “chưa kiểm tra được”, không kết luận sạch lỗ hổng.

## 8. Hoàn thiện trải nghiệm và tính ứng dụng

### Nhân viên

Trang đầu trả lời ba điều: máy đang được hỗ trợ là máy nào, hiện có sự cố gì, cần bấm gì tiếp theo. Ưu tiên “Báo sự cố”, “Nhờ cài phần mềm”, “Xem yêu cầu”. Nếu chỉ có một máy hợp lệ thì tự chọn; nhiều máy thì hiển thị tên dễ nhận biết và nơi sử dụng. Không tự chọn sai khi thiếu dữ liệu.

Mỗi màn hình có trạng thái đang tải, trống, lỗi, hết phiên và mất kết nối. Giữ nội dung nhập khi gửi lỗi, chống bấm gửi hai lần, cho biết đã gửi hay chưa. Hiển thị thời gian địa phương cùng độ mới dữ liệu; dữ liệu cũ không tô xanh như đang hoạt động.

Ngôn ngữ: “Không kết nối được máy chủ”, “Mã đã được sử dụng”, “IT đang xử lý”, “Cần khởi động lại để hoàn tất”. Chi tiết kỹ thuật nằm trong mục mở rộng dành cho IT. Thao tác phá vỡ công việc phải báo tác động, yêu cầu xác nhận phù hợp và cho hủy trước khi gửi.

Ảnh hỗ trợ phải xem trước. Không yêu cầu nhân viên chọn severity; dùng câu hỏi có tiếp tục làm việc được không, triệu chứng và số người bị ảnh hưởng để hỗ trợ IT đánh giá.

### IT và Admin

Dashboard ưu tiên việc cần xử lý: máy mất kết nối, nhân viên bị gián đoạn, yêu cầu chờ duyệt/quá hạn. Từ một yêu cầu đi được đến máy, lịch sử và thao tác hợp lệ; không phải nhập lại device ID. Mọi nút không khả dụng phải nêu lý do như offline, thiếu quyền hoặc Agent không hỗ trợ.

Bộ lọc và tìm kiếm giữ được khi quay lại. Trạng thái có chữ/icon ngoài màu; hỗ trợ bàn phím, focus rõ, label và thông báo lỗi đọc được. Kiểm thử trên kích thước laptop thực tế, màn hình hẹp và zoom 200%.

Tổ chức thử với 3–5 người không tham gia phát triển nếu có thể: báo lỗi, gửi ảnh, xem phản hồi, tìm máy offline. Ghi tỷ lệ hoàn thành, thời gian và điểm vướng; không tuyên bố dễ dùng chỉ từ đánh giá của người viết mã.

## 9. Kế hoạch kiểm thử và tiêu chí nghiệm thu

Mỗi Fxx có bộ dữ liệu, điều kiện trước, bước thao tác, kết quả mong đợi, bằng chứng và liên kết test/commit. Hạng mục chỉ được đánh dấu đạt khi đường thành công, quyền và ngoại lệ chính đều đạt.

| Nhóm | Bằng chứng bắt buộc |
|---|---|
| Domain/Application | Chuyển trạng thái, phân quyền, expiry/nonce, idempotency, xung đột |
| PostgreSQL thật | Migration từ DB mới và bản trước; tranh chấp token/OTP/assignment; transaction rollback; tenant |
| API/Web | Vai trò đúng/sai, ID người khác, cookie/CSRF, validation, lỗi server và trạng thái UI |
| Agent giả lập | Retry/backoff, giới hạn queue, telemetry thiếu, đồng hồ lệch, chữ ký sai, replay, credential thu hồi |
| Windows thật | Cài mới, reboot, service recovery, sleep/wake, Wi-Fi/LAN, mất mạng, nâng cấp/gỡ trên máy lab |
| Vận hành | Cài server mới từ hướng dẫn; backup/restore sang môi trường khác; mất Internet nội bộ; hết hạn chứng chỉ |
| Đặc quyền | Bản mặc định không thực thi thao tác lab; bản opt-in chỉ tác động thiết bị lab được cho phép |
| Chất lượng giao diện | Keyboard, focus, lỗi/trống/loading, responsive, zoom; thử tác vụ với người dùng |

Mục tiêu đo đề xuất, phải hiệu chỉnh theo cấu hình lab và công bố kết quả thật:

- Kiểm thử vận hành tối thiểu hai Agent Windows; dùng laptop thứ ba nếu có để tách rõ server, IT và nhân viên.
- Tải mục tiêu 50 Agent giả lập bên cạnh Agent thật; tăng lên 100 để tìm giới hạn, không tuyên bố 100 máy thật đã được nghiệm thu.
- API đọc phổ biến p95 dưới 1 giây trên cấu hình lab công bố; thao tác bất đồng bộ đo riêng thời gian nhận và hoàn tất.
- Agent rảnh: CPU trung bình mục tiêu dưới 2%, RAM mục tiêu dưới 200 MB; đo trong phiên chạy ổn định và nêu phần cứng/cách lấy mẫu.
- Trạng thái offline dựa trên ngưỡng cấu hình, ví dụ 120 giây; cập nhật sau ngưỡng trong một chu kỳ xử lý. Không coi một heartbeat trễ là sự cố.
- Chạy liên tục ít nhất 24 giờ, có ngắt mạng, sleep/wake và restart server; không nhân đôi đăng ký, không tăng queue không giới hạn.
- Backup mặc định mỗi ngày: RPO mục tiêu 24 giờ, RTO mục tiêu 2 giờ trong lab. Đo phục hồi thật trước khi công bố đạt.
- Không còn lỗi chặn luồng P0, vượt quyền hoặc lộ secret đã biết chưa xử lý. Các giới hạn còn lại có tài liệu và không bị trình bày như tính năng hoàn thiện.

Các con số trên là tiêu chí đề xuất, không phải số đo hiện tại hay cam kết hiệu năng cho mọi công ty.

## 10. Thứ tự triển khai theo cổng nghiệm thu

Làm lần lượt từng gói, hoàn thành cả API, Agent/web, test và tài liệu của gói đó. Một người làm nên hạn chế một gói đang triển khai và một gói đang nghiệm thu.

| Giai đoạn | Công việc cụ thể | Điều kiện ra khỏi giai đoạn |
|---|---|---|
| G0 — Chốt đặc tả | Đối chiếu mã/tài liệu, backlog F01–F24, ma trận quyền, state machine, ADR self-host và enrollment | Mỗi luồng có người thực hiện, quyền, dữ liệu và tiêu chí kiểm tra; không còn mô tả trái nhau |
| G1 — Nền tảng công ty | Profile Compose LAN, bootstrap Admin, tắt route/service Platform ở server trong profile này, DNS/TLS, cấu hình dev/test/staging | Cài mới không cần Platform Owner; chạy từ hai laptop trong LAN khi Internet ngắt |
| G2 — Danh tính và bộ cài | Bộ cài công ty, opaque token, đăng ký nguyên tử, credential lifecycle, trạng thái setup, nâng cấp/cài lại | Cài trên Windows sạch; hai máy dùng cùng token chỉ một máy đăng ký; thất bại có đường phục hồi |
| G3 — Kênh Agent tin cậy | Chữ ký bất đối xứng, bootstrap/rotation, nonce/expiry, IPC, LocalService, heartbeat và thu hồi | Sửa lệnh/replay/sai thiết bị bị từ chối; thu hồi có hiệu lực; không cần nhúng shared signing secret |
| G4 — Quản lý máy và giám sát | Bàn giao, inventory, dashboard, dữ liệu cũ/offline, quy tắc cảnh báo tối thiểu, audit | Hai máy thật hiển thị đúng; đổi người nhận và mất mạng không làm sai quyền/lịch sử |
| G5 — Hỗ trợ xuyên suốt | Một cửa báo lỗi, ảnh có đồng ý, hàng đợi IT, hội thoại, lịch sử, resolve/close/reopen | Nhân viên gửi → IT xử lý → nhân viên xác nhận; test truy cập chéo và gửi lặp đạt |
| G6 — Thao tác có kiểm soát | Catalog phần mềm, duyệt, maintenance OTP, trạng thái command, IT thủ công khi cần nâng quyền | Không nhầm đã duyệt với đã thực hiện; giả gói/sai OTP/sai quyền bị chặn; bảo trì tự hết hạn |
| G7 — Vận hành và phát hành | Retention, backup/restore, logging/health, migration, nâng cấp Agent/server, hướng dẫn xử lý sự cố | Khôi phục được trên VPS 2/lab độc lập; cài lại theo tài liệu; kiểm tra 24 giờ đạt |
| G8 — Chiều sâu P1 | Policy có phiên bản, asset/loan, lịch hẹn/thông báo/help, QR, báo cáo, MFA; broker nếu cần | Mỗi tính năng đạt checklist riêng; không làm hỏng P0; chưa đạt thì ghi rõ và không đưa vào demo chính |
| G9 — Hoàn thiện đồ án | Kiểm thử người dùng, tối ưu theo số đo, đối chiếu tài liệu, báo cáo và kịch bản bảo vệ | Một bản release có checksum, bằng chứng test, giới hạn rõ và demo có thể lặp lại |

G2 và G3 cùng là điều kiện trước khi phân phối Agent rộng hoặc thử thao tác đặc quyền. Có thể triển khai thao tác phần mềm thủ công trước broker; không bỏ xác minh nguồn gói để rút ngắn công việc.

Các vùng mã chính để triển khai: `apps/backend/src` (use case/API/EF), `apps/agent/src` (Core/Infrastructure/Worker/Setup), `packages/enrollment` (contract mã), `packages/web-ui` (UI và typed client), `apps/company`/`apps/employee` (portal), `deploy/agent` (MSI), `deploy`/Compose (triển khai), các test tương ứng và tài liệu ADR/OpenAPI. Xác định symbol thực tế qua CodeGraph trước mỗi thay đổi; không coi danh sách này là chỉ dẫn sửa toàn bộ cùng lúc.

## 11. Hồ sơ đồ án và kịch bản bảo vệ

Hồ sơ cần có: bài toán công ty nhỏ, phạm vi và đối tượng; sơ đồ triển khai; use case; ERD; sequence đăng ký/hỗ trợ/thực thi; state machine; ma trận quyền; threat model; ADR; OpenAPI; hướng dẫn triển khai/nhân viên/IT; kế hoạch backup/restore; bảng kiểm thử và số đo; giới hạn và hướng phát triển.

Demo chính khoảng 12–15 phút, dùng dữ liệu giả:

1. Giới thiệu server công ty trong LAN, ba vai trò và hai laptop.
2. Admin cấp token; cài/kết nối Agent trên máy lab; thiết bị xuất hiện sau heartbeat.
3. Dùng lại token trên lần đăng ký khác và chứng minh bị từ chối.
4. Giao máy cho nhân viên; nhân viên báo sự cố bằng ngôn ngữ thường, chọn ảnh và gửi.
5. Technician nhận yêu cầu, trao đổi, gửi một thao tác an toàn có sẵn; xem kết quả và audit.
6. Giải quyết/đóng yêu cầu; cho thấy phê duyệt không tự biến thành kết quả thành công.
7. Ngắt Internet bên ngoài trong khi LAN còn hoạt động; chứng minh hệ thống nội bộ vẫn dùng được. Ngắt riêng mạng Agent để minh họa offline rồi kết nối lại.
8. Thu hồi credential hoặc xem bằng chứng kiểm thử thu hồi; kết thúc bằng báo cáo đo và bằng chứng backup/restore.

Những việc dài như tạo VM, cài MSI cần reboot và phục hồi DB nên chuẩn bị snapshot và bản ghi bằng chứng, trình bày rõ bước nào trực tiếp/bước nào ghi trước. Không dùng mô phỏng như bằng chứng thao tác Windows thật. Không demo khóa/cô lập trên máy đang dùng bảo vệ.

## 12. Định nghĩa hoàn thành

Bản đồ án được coi là hoàn thành khi toàn bộ P0 đạt trên môi trường nêu rõ; các P1 được công bố hoàn thiện cũng phải có bằng chứng; không còn mâu thuẫn giữa UI, API và tài liệu; người khác có thể triển khai theo hướng dẫn; nhân viên hoàn thành tác vụ chính; dữ liệu có thể phục hồi; quyền và token/lệnh chịu được các tình huống kiểm thử đã định.

Mục tiêu là một sản phẩm Windows cho công ty tự quản lý, có vòng đời vận hành đầy đủ và chứng minh được. macOS/mobile/SaaS tiếp tục là hướng mở rộng có ranh giới, không làm loãng nghiệm thu của bản này.
