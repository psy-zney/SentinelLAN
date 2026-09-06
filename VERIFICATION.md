# Kiểm tra chức năng và điều kiện chuyển tuần — 06/09/2026

## Kết luận

Week 2 đã được sửa và kiểm chứng ở môi trường Development/InMemory, nhưng **chưa được nghiệm thu toàn bộ**: máy này chưa có Docker/PostgreSQL để chạy migration và các ràng buộc trên PostgreSQL thật. Vì yêu cầu chuyển Week 3 có điều kiện, chưa đánh dấu Week 2/Week 3 hoàn tất. Trong đợt rà soát đã sửa thêm nền tảng safe commands để giảm việc tồn đọng Week 3.

## Ma trận chức năng

| Nhóm | Kết quả kiểm tra | Bằng chứng / giới hạn |
|---|---|---|
| Login, session, refresh rotation, logout | Đạt local | AuthenticationServiceTests, AuthenticationApiTests, auth-lifecycle.e2e.ts |
| Admin/Technician/Employee/Agent | Đạt các ca hiện có | PermissionTests; API từ chối Employee tạo lệnh, Technician đọc audit; browser Employee chỉ vào My Device |
| Tenant và assigned device | Đạt API local | DeviceLifecycleIntegrationTests và AuthenticationApiTests; SignalR dùng group organization, chưa có E2E hai tenant cho hub |
| Enrollment hợp lệ, hết hạn, dùng lại | Đạt local | Audit thành công/thất bại có tenant; token không nhận diện được trả 400, không suy đoán tenant để audit |
| Enrollment đồng thời | Đạt local | DeviceConcurrencyTests; UsedAt có optimistic concurrency; PostgreSQL chưa chạy |
| Heartbeat, retry, idempotency | Đạt local | Payload có validation; gửi trùng/đồng thời giữ một snapshot; Agent giữ nguyên key và mẫu khi retry |
| CPU/RAM/disk | Đạt Windows local | CPU delta toàn hệ thống, RAM vật lý, system volume; test parser Linux đạt, chưa chạy Agent trên Linux thật |
| Online/offline + SignalR | Đạt browser local | Thêm thiết bị không reload; Offline → Online; ngừng heartbeat hai phút → Offline; LastSeenAt không bị làm mới giả |
| Inventory và detail | Đạt browser local | Dữ liệu API thật; telemetry thay đổi trên trang; bỏ nuốt lỗi telemetry |
| Dashboard | Đạt luồng dữ liệu local | Bỏ demo fallback; cập nhật số đếm khi nhận device-status |
| Employee transparency | Đạt luồng hiện có | Thiết bị gán, tên policy seed và 20 audit gần nhất; không phải lịch sử không giới hạn |
| Tạo/nhận lệnh mô phỏng | Đạt API local + browser | 5 loại allow-list, reason, confirmed=true, expiry, nonce; không có shell/RestartService/khóa thật |
| Chữ ký và replay | Đạt integration local | Agent kiểm chứng HMAC thật, sai thiết bị/chữ ký/tenant/reason/replay/expiry bị chặn; thiếu key không polling |
| Command result | Đạt local | Chặn result trước delivery/hết hạn; retry giữ receipt cũ; hai lệnh cùng loại không đụng audit |
| Audit | Đạt API và EF local | Append completion thay vì sửa creation; EF chặn UPDATE/DELETE; database privilege và UI audit còn thiếu |
| Policy CRUD/assignment | Chưa có | Có entity/seed; GET policies trả danh sách rỗng; trang UI là placeholder |
| Command center | Chưa đầy đủ | Dispatch SimulateLock có tại detail; trang /commands vẫn placeholder |
| Alerts | Chưa đầy đủ | Có model và dashboard count; acknowledge/resolve, rule engine và UI chưa có |
| Users/organizations | Chưa có CRUD | GET hiện trả mảng rỗng, giao diện Users placeholder |
| Windows service install, protected identity, queue bền vững | Chưa nghiệm thu | Worker composition có; file identity, nonce cache/retry hiện process-local; còn Week 5 |
| PostgreSQL/Compose/CI | Chưa nghiệm thu | Không tìm thấy Docker, PostgreSQL service hoặc .github trong checkout hiện tại |

## Thay đổi chính

- Thêm validation enrollment/heartbeat, concurrency token và migration mới cho UsedAt/command Status.
- Phát sự kiện availability mỗi khi trạng thái thay đổi; UI đọc lại dữ liệu server khi nhận event/reconnect.
- Rate limit tách theo endpoint và actor/device/IP thay vì một bucket chung cho cả hệ thống.
- Sửa chữ ký để bao gồm tenant/actor/reason và ổn định qua độ chính xác timestamp PostgreSQL.
- Bắt buộc xác nhận lệnh, không dispatch tới device revoked; bảo vệ trạng thái delivery/result và audit append-only.
- Agent giữ heartbeat/result chờ retry trong bộ nhớ. Mất response polling và restart vẫn cần cơ chế delivery bền vững ở Week 5.
- Đo CPU/RAM hệ thống bằng adapter Windows/Linux; không thêm telemetry ngoài phạm vi công bố.
- Bổ sung scripts/test-all.ps1 và scripts/test-all.sh; cập nhật tài liệu phản ánh đúng chức năng hiện có.

## Bằng chứng kiểm tra

- .NET Release build: đạt, không lỗi biên dịch.
- .NET: **54/54** (Domain 4, Application 14, Architecture 1, Agent 8, Integration 27), provider InMemory.
- Web lint/typecheck và production build: đạt; Vitest **7/7**. Format .NET và git diff --check đạt.
- Browser: **3/3 E2E đạt trong 2,3 phút** ở lần chạy đầy đủ cuối: Employee auth, enrollment → realtime → command → audit → offline, và hiển thị/phục hồi lỗi API. Chạy ngoài sandbox Windows đã tự dừng sạch các server; sandbox hạn chế taskkill /T khiến bước dọn server bị treo ở lần trước.
- npm audit: **0 vulnerabilities**.
- NuGet vulnerability listing: không liệt kê package có lỗ hổng; restore/build có NU1900 khi tải dữ liệu vulnerability, nên chưa xem đây là chứng nhận sạch từ nguồn online.
- PostgreSQL: **chưa chạy**. Hai migration mới chỉ thay đổi metadata concurrency, cần kiểm chứng trên Npgsql thật.

Các test browser dùng API/Agent request giả lập client; chưa phải nghiệm thu Worker Windows Service cài trên máy lab. Các thao tác khóa/cô lập hệ điều hành thật chưa được thực thi.

## Thứ tự tiếp theo

1. Cấp một PostgreSQL test database cô lập qua ConnectionStrings__SentinelLAN; chạy scripts/test-all.ps1 -RequirePostgres. Không dùng database sản xuất: test có seed dữ liệu.
2. Xác minh migration, single-use enrollment, unique heartbeat/nonce/result và command concurrency trên PostgreSQL; sau đó mới đóng gate Week 2.
3. Week 3: triển khai Policy use cases trong Application, ports/store ở Infrastructure, API CRUD/assignment có tenant/permission và UI thật; thêm test tenant, invalid input và assignment.
4. Hoàn thiện command center; giữ mô phỏng mặc định. Chỉ xem xét adapter lab thật khi có thiết bị và phạm vi lab được chỉ định.

## Tài liệu kỹ thuật

Cách đo Windows dựa trên [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes) và [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex); Linux dùng [proc filesystem](https://docs.kernel.org/filesystems/proc.html). Windows trên 64 logical processors cần thêm xử lý processor group; số liệu Linux chưa phản ánh riêng cgroup/container.

Lưu ý tích hợp: API và Agent cần cùng SENTINELLAN_SIGNING_KEY để xác minh HMAC trong MVP; không ghi key vào repository. Payload chữ ký đã thay đổi, lệnh cũ phải hết hạn và được tạo lại sau khi cập nhật đồng bộ. HMAC dùng chung key chưa đáp ứng production; chuyển bất đối xứng ở Week 5. Tài liệu trong docs/ và script trong scripts/ đang bị .gitignore bỏ qua; VERIFICATION.md này nằm ở root để được review cùng code.
