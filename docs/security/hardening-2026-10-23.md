# Nâng cấp bảo mật và truy vấn — 23/10/2026

> Cập nhật nghiệm thu 24/10/2026: xem [báo cáo sửa lỗi và verification cuối](../reviews/remediation-verification-2026-10-24.md). Các kết quả đo ở phần cũ bên dưới là ảnh chụp trước vòng sửa lỗi.

Phạm vi: API ASP.NET Core, PostgreSQL/EF, dashboard web, mobile và cấu hình reverse proxy trong repository. Đây là bản thay đổi mã nguồn và kế hoạch nghiệm thu; chưa triển khai production, chưa kiểm chứng firewall trên máy thật. Các sửa giao diện/phụ thuộc có sẵn trong working tree được giữ nguyên.

## Biện pháp đã thực hiện

Bổ sung hợp đồng ngày 24/10: `DELETE /api/v1/users/{id}/activation-token` và `DELETE /api/v1/devices/{id}/qr-label` yêu cầu body JSON có lý do không rỗng và `confirmed=true`; thiếu/null hoặc không xác nhận trả 400, không sửa dữ liệu/audit. OpenAPI đánh dấu request body bắt buộc. `PUT /api/v1/work-orders/{id}/complete`, `POST /api/v1/asset-loans`, `PUT /api/v1/asset-loans/{id}/return` trả 409 khi xung đột với trạng thái hiện tại; OpenAPI công bố phản hồi này. Tạo work order chỉ chấp nhận `Corrective`/`Preventive` và `Low`/`Medium`/`High`/`Critical`.

Hai portal dùng nonce CSP riêng từng response và render động, cache tài liệu `private, no-store`; proxy ghi đè nonce/CSP đầu vào và Nginx không đặt thêm CSP trái policy upstream. Production không cho script inline không nonce hoặc `unsafe-eval`. Style inline còn được cho phép để tương thích React/Motion. API origin phải là HTTP(S), không mang username/password; connect-src gồm đúng origin HTTP/WebSocket cần thiết. Đây không thay thế kiểm chứng HTTPS/firewall ở edge thật.

| Vấn đề | Thay đổi | Giới hạn cần biết |
| --- | --- | --- |
| Lạm dụng API trước xác thực | Ingress middleware chặn theo IP và số request đồng thời trước authentication; trả 429 và Retry-After | Mỗi process có ngân sách riêng; cần lớp edge khi chạy nhiều replica hoặc chống DDoS |
| Dò mật khẩu qua web/mobile/nhiều IP | Bucket theo công ty/email chuẩn hóa trong DB, khóa transaction PostgreSQL, backoff tăng dần | Kẻ biết email có thể gây chậm đăng nhập tạm thời; không khóa tài khoản vĩnh viễn |
| Dò tồn tại tài khoản | Nhánh tài khoản không tồn tại/inactive chạy PBKDF2 giả, lỗi đăng nhập chung | Giảm khác biệt KDF; chưa có đo phân bố timing dưới tải thực tế |
| Gửi hash thay cho mật khẩu | Tiếp tục kiểm tra mật khẩu gốc bằng PBKDF2, thêm kiểm thử gửi nguyên hash bị từ chối; production tắt xác thực SHA256 cũ mặc định | Không chặn tùy tiện mọi chuỗi trông giống hash; không thay thế bảo vệ NTLM/LSASS của Windows |
| Logout-all chưa vô hiệu access token | Đổi security stamp, thu hồi refresh cùng lần lưu; đóng kết nối realtime qua guard | Realtime có kiểm tra định kỳ; các replica khác cần chờ cơ chế revalidation hiện có |
| Mobile báo logout-all thành công khi server lỗi | Xác nhận HTTP thành công rồi mới xóa session; hỗ trợ refresh/retry; hiển thị lỗi rõ ràng | Nếu refresh bị thu hồi, client phải đăng nhập lại |
| Request/connection gây cạn tài nguyên | Body API/Nginx 4 MiB, timeout đọc header 15 giây, tối đa 8 kết nối dashboard/user và 2 Agent/device | Không thay thế WAF/giới hạn kết nối TCP ở edge |
| N+1 | User activation dùng EXISTS; asset tham chiếu user/device theo lô; request dùng batch command/result; timeline batch user; health một query | Worker đối soát còn đọc lịch sử công ty; chi tiết tài sản vẫn tổng hợp lịch sử đầy đủ |
| Danh sách tải toàn bộ rồi lọc | User/device/incident/work-order/loan/request/message/notification phân trang SQL sau tenant/owner | Các danh sách catalog/announcement và một số lịch sử khác chưa có phân trang; cần đo dữ liệu thực |
| Ảnh làm đầy lưu trữ | Quota người/ngày, công ty/ngày, tổng công ty; serialize kiểm tra và lưu bằng tenant write lock | Dữ liệu vượt quota cũ không bị xóa, tổng quota cần quy trình lưu trữ thủ công |
| Mutation có audit ghi sau, khác transaction | Sáu mutation tài sản bọc transaction gồm dữ liệu và audit | Test rollback thật cần PostgreSQL; InMemory không có bảo đảm tương đương |
| Proxy LAN không được tin đúng địa chỉ | IP proxy LAN cố định, mapping KNOWN_PROXIES; edge ghi đè X-Forwarded-For; regex auth không phân biệt hoa thường | Cần xác nhận proxy chain/đường truy cập thực tế và CIDR không xung đột |
| Advisory của Next.js và tooling | Hai portal dùng 16.4.2; cập nhật Vitest/Vite và bản vá Expo, xác minh source backport cho dependency chưa nâng tương thích | Gate cả runtime/dev không còn advisory Moderate+ chưa vá; raw audit theo version vẫn có thể báo các bản đã backport |

## Cấu hình và hành vi

Tất cả giới hạn byte dưới đây dùng byte, không phải số ký tự/base64. Ngày quota tính từ 00:00 UTC. Mỗi attachment tính `max(size, 4096)`; file vẫn tối đa 2 MiB, PNG/JPEG kiểm tra magic bytes, tối đa 10 ảnh/yêu cầu. Vượt quota ngày trả 429, vượt tổng lưu trữ trả 409; không ghi ảnh bị từ chối.

| Biến môi trường | Mặc định | Phạm vi |
| --- | --- | --- |
| `SENTINELLAN_INGRESS_REQUESTS_PER_MINUTE` | 600 | Token nạp mỗi phút/IP/process, 30–60000 |
| `SENTINELLAN_INGRESS_BURST` | 300 | Dung lượng bucket/IP/process, 1–10000 |
| `SENTINELLAN_INGRESS_CONCURRENCY` | 128 | Request API đồng thời/process, 1–4096; realtime có pool riêng 512 |
| `SENTINELLAN_ATTACHMENT_USER_DAILY_BYTES` | 20971520 | 20 MiB/người/ngày |
| `SENTINELLAN_ATTACHMENT_ORGANIZATION_DAILY_BYTES` | 209715200 | 200 MiB/công ty/ngày |
| `SENTINELLAN_ATTACHMENT_ORGANIZATION_TOTAL_BYTES` | 2147483648 | 2 GiB/công ty |
| `SENTINELLAN_LEGACY_PASSWORD_HASHES_UNTIL` | trống | Production từ chối SHA256 cũ; nếu cần chuyển đổi, hạn ISO UTC không quá 7 ngày kể từ khởi động |

Quota phải dương, người/ngày ≤ công ty/ngày ≤ công ty/tổng ≤ 1 TiB. Sau xác thực, user/device có bucket 300 request/phút; route nhạy cảm có giới hạn riêng 30/phút. Nginx LAN/VPS có auth zone 10 request/phút và burst 10 cho login/refresh/activation. Nhiều nhân viên cùng NAT có thể chạm ngân sách IP: đo lưu lượng và điều chỉnh API/edge đồng bộ, giữ bucket tài khoản độc lập.

Backoff dùng chung web và mobile: năm lần thử được nhận ban đầu; lần kế tiếp phải chờ một giây, sau đó hai, bốn… tối đa một phút. Request bị chặn không kéo dài thời hạn chờ. Bucket hết hạn sau 15 phút kể từ lần thử được nhận gần nhất, được worker retention dọn; không ghi email/password thô vào bảng này. Hash định danh không phải ẩn danh hoàn toàn vì email có thể đoán được.

## Hợp đồng API thay đổi

DELETE `/api/v1/users/{id}/activation-token` và `/api/v1/devices/{id}/qr-label` yêu cầu JSON body có `reason` dài 3–1000 ký tự sau khi trim và `confirmed: true`. Thiếu body, JSON `null`, thiếu lý do hoặc chưa xác nhận trả 400; không thu hồi đối tượng và không ghi audit thành công. Server không tự điền lý do hoặc xác nhận thay người thực hiện.

Ngân sách ingress bổ sung tín dụng liên tục theo thời gian đơn điệu với tốc độ `SENTINELLAN_INGRESS_REQUESTS_PER_MINUTE`, giữ phần lẻ giữa các request. `SENTINELLAN_INGRESS_BURST` chỉ giới hạn số tín dụng tích lũy; tốc độ dài hạn không bị cắt xuống bằng burst. Các ngân sách vẫn cục bộ theo process và IP.

GET `/api/v1/users`, `/devices`, `/incidents`, `/work-orders`, `/asset-loans`, `/self-service/requests`, `/self-service/requests/{id}/messages`, `/self-service/notifications` nhận `offset` (mặc định 0, tối đa 1000000) và `limit` (mặc định 200, 1–200). Vẫn trả array như trước; tham số ngoài khoảng trả 400. Client tăng offset theo limit cho tới trang ngắn hơn limit. Tin nhắn lấy trang mới nhất trước, thứ tự trong mỗi trang tăng theo thời gian. Mỗi danh sách có tie-breaker ID để ổn định khi timestamp trùng nhau.

`GET /devices` có thêm `q` tối đa 100 ký tự (tên/OS) và `status=all|online|offline|revoked`. `GET /self-service/requests?active=true` loại Closed/Rejected trước phân trang. Dashboard trả tổng số chính xác và tối đa 200 thiết bị; có liên kết sang danh sách đầy đủ. Web/mobile có điều khiển trang cho các danh sách chính. Các client bên ngoài phải nâng cấp nếu trước đây giả định một response chứa toàn bộ dữ liệu. Các schema/parameter OpenAPI được sinh từ endpoint hiện tại.

## SQL injection và bảo vệ biên tin cậy

Các thay đổi dùng LINQ tham số hóa hoặc `ExecuteSqlInterpolatedAsync` cho advisory lock; không ghép input vào SQL. Expression index là SQL cố định trong migration. Bộ kiểm tra Npgsql xác nhận SQL sinh ra có tenant/owner, paging, không chọn PasswordHash khi liệt kê user, không đọc Content khi tính quota. Việc dịch SQL không thay thế EXPLAIN/kiểm thử trên PostgreSQL thật. Chưa có kiểm định pentest độc lập hay kết luận toàn bộ repository không có SQL injection.

Role, tenant và thiết bị đang phân công tiếp tục là điều kiện truy cập. Không thêm endpoint thực thi shell, đọc thông tin đăng nhập hoặc chụp màn hình bí mật. Real lock/isolation vẫn bị giới hạn bởi opt-in lab và device allowlist hiện có.

## Trình tự nâng cấp và nghiệm thu

1. Sao lưu PostgreSQL và thử restore trên môi trường staging tách biệt. Kiểm kê tài khoản dùng hash cũ bằng công cụ quản trị được phép; chỉ đếm/phân loại, không xuất hash. Lập kế hoạch reset/kích hoạt lại hoặc cửa sổ chuyển đổi ngắn. Không thể nâng cấp SHA256 bằng cách hash lại chuỗi hash hiện có.
2. Rà soát cấu hình quota/budget, subnet proxy LAN `172.30.20.0/24` và VPS `172.30.10.0/24`. Chỉ tin IP/CIDR proxy thật; không dùng `0.0.0.0/0`. Với chuỗi proxy nhiều hop, kiểm tra địa chỉ client cuối tại API. Cấu hình edge phải tương thích 4 MiB.
3. Dừng nhận traffic trong maintenance window, áp dụng migration mới `20261022175225_SecurityAndBoundedQueries`, sau đó khởi động phiên bản API/client đồng bộ. API có thể tự migrate khi khởi động theo cơ chế hiện có, nhưng cần kiểm soát rollout. Tạo index trên bảng lớn có thể khóa ghi, cần đo trên staging. Không rollback schema khi phiên bản mới còn nhận traffic.
4. Với PostgreSQL disposable: chạy integration tests, đặc biệt quota upload đồng thời, account backoff giữa hai API instances, rollback khi insert audit lỗi, tenant/owner trước phân trang, cleanup không xóa bucket vừa được gia hạn. Kiểm tra `EXPLAIN (ANALYZE, BUFFERS)` với dữ liệu đại diện; chưa có bằng chứng tốc độ thực tế ở quy mô production.
5. Nghiệm thu web/mobile: đăng nhập đúng/sai, 429 + Retry-After, logout-all làm phiên khác mất hiệu lực, server 500 không báo logout thành công, xem trang lịch sử, gửi tin trên trang cũ, upload chạm quota. Kiểm tra 1/NAT và nhiều IP trên lab với số request hữu hạn.
6. Hạ tầng: DB/Redis/API không public trực tiếp; chỉ edge công khai HTTPS, admin qua VPN/IP allowlist; tài khoản DB runtime tối thiểu quyền và tách tài khoản migration; kiểm tra TLS, log redaction, backup và alert. Repository không chứng minh được cấu hình firewall đang chạy.
7. Windows lab được phép: kiểm tra Agent không tự nâng quyền, service ACL, signature/nonce/replay, firewall profile và recovery khi mất kết nối. Không thử phá firewall/NTLM trên máy nhân viên hoặc môi trường thật. Chống pass-the-hash cấp OS cần Credential Guard/LSA protection, giảm NTLM theo tính tương thích và tránh dùng chung local-admin password; đánh giá riêng trước rollout.

## Đề xuất tiếp theo, chưa triển khai

- MFA/WebAuthn cho Admin; step-up authentication cho thay đổi quyền, enrollment và thao tác nhạy cảm. Cần thiết kế recovery và chống chiếm quyền trong bước đăng ký MFA.
- Shared edge rate limiting/WAF khi có nhiều API replica; metric 401/429, thời gian KDF, lock wait, lượng attachment và DB query; cảnh báo dựa trên dữ liệu đo thay vì mặc định vô hạn mức.
- Cursor pagination và batch có checkpoint cho worker đối soát/health ở tenant lớn; partition/retention cho telemetry; workflow lưu trữ ảnh có audit và xác nhận, không tự xóa dữ liệu nghiệp vụ.
- CI PostgreSQL riêng biệt bắt buộc cho transaction/concurrency, dependency advisory + kiểm tra backport hiện có, pentest độc lập và diễn tập restore/recovery có quyền rõ ràng.

## Kết quả xác minh

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet build SentinelLAN.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | Đạt; 0 cảnh báo, 0 lỗi |
| EF `has-pending-model-changes` và sinh SQL migration offline | Không có model change chưa có migration; sinh script thành công, không áp dụng DB |
| Nhóm mới bảo mật/query/quota ở lượt đầu | 17 đạt, 1 ca rollback PostgreSQL skip; sau đó thêm ca concurrency PostgreSQL |
| Domain/Application/Architecture trong lượt test toàn solution | 23/65/1 ca đạt trước lần rebuild cuối |
| Backend integration, lượt hồi quy cuối gồm 76 ca | 19 đạt, 55 lỗi do Windows Application Control chặn nạp SentinelLAN.Application.dll (0x800711C7), 2 ca PostgreSQL skip; thử ngoài sandbox vẫn bị host policy chặn. Chưa xác nhận toàn bộ backend ở trạng thái cuối |
| `npm run lint`, `npm run lint:mobile`, `npm run typecheck`, `npm run typecheck:mobile` | Đạt cho company/employee/web-ui/mobile sau nâng Next.js |
| Vitest web-ui / Jest mobile | 44/44 và 76/76 ca đạt |
| `npm run build` | Cả company và employee đạt với Next.js 16.4.2; chạy ngoài sandbox sau lỗi EPERM khi prerender trong sandbox |
| `npm run audit:dependencies` | Đạt: không còn High/Critical chưa vá theo verifier; còn 3 Moderate, 12 ca kiểm tra backport đạt |
| NuGet vulnerable, gồm transitive | Hoàn tất, không liệt kê advisory cho 14 project đã restore |
| Docker Compose config LAN/VPS, không nội suy/nạp .env | Đạt; chưa kiểm chứng runtime Nginx/TLS/proxy |
| Format C# / `git diff --check` | Format whitespace bằng folder mode đạt; kiểm tra whitespace diff đạt. Solution mode của formatter bị chặn named pipe trong sandbox |

Các ca PostgreSQL thật bị skip khi không cung cấp connection string; Docker Desktop daemon hiện chưa chạy. Kiểm thử dịch Npgsql không mở kết nối DB. Không coi kết quả InMemory là bằng chứng rollback/khóa PostgreSQL. Full Agent suite còn ca cần quyền Windows, PowerShell và DLL mà host hiện tại chặn. Cần CI/staging được phép chạy code, PostgreSQL disposable và lab Windows trước khi phát hành; không vô hiệu host security policy để làm test xanh.
