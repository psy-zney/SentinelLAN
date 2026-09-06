- https://github.com/psy-zney/SentinelLAN.git
đánh tính khả thi, tính ứng dụng, tính cạnh tranh

    điểm mới, điểm cũ, điểm chưa nghĩ đến, điểm khuất công nghệ :
    1. Đánh giá Tính khả thi (Feasibility)

    Về mặt Công nghệ (Rất cao):

        Stack lựa chọn rất thực tế và chuẩn công nghiệp: ASP.NET Core (Modular Monolith) + PostgreSQL + Next.js (React/TypeScript) + .NET Windows Service. Đây là bộ khung chịu tải tốt, type-safe từ đầu đến cuối và dễ bảo trì.

        Kiến trúc Domain-Driven/Clean Architecture (tách Application, Domain, Infrastructure) giúp hạn chế "spaghetti code", kiểm soát phụ thuộc 1 chiều tốt.

    Về tiến độ 6 tuần (Thách thức lớn):

        Tuần 1 đã xong auth, nhưng từ Tuần 2 đến Tuần 6 là khối lượng công việc khổng lồ: Tích hợp SignalR realtime hai chiều, mã hóa phi đối xứng, chống replay, Windows Service lifecycle, offline sync, và audit trail.

        Nút thắt thực tế: Việc chưa có PostgreSQL runtime local mà chỉ dựa vào CI/InMemory sẽ gây ra các lỗi tiềm ẩn khi chuyển sang chạy production thật (như khác biệt về transaction locking, timezone UTC handling, và concurrency token giữa EF Core InMemory vs Npgsql).

2. Đánh giá Tính ứng dụng (Applicability)

    Khách hàng mục tiêu phù hợp:

        Doanh nghiệp vừa và nhỏ (SMEs), trường học, lab công nghệ: Nơi không đủ ngân sách mua license đắt đỏ của Microsoft Intune, ManageEngine hay CrowdStrike, nhưng cần quản lý tập trung danh mục thiết bị (Hardware asset management) và giám sát trạng thái cơ bản.

        Môi trường yêu cầu cao về quyền riêng tư (Privacy-first / GDPR compliant): Triết lý của SentinelLAN cực kỳ đáng giá ở chỗ: Nói không với giám sát xâm phạm (không keylogger, không chụp màn hình, không đọc lịch sử web, không nghe lén mic/cam). Có riêng màn hình Employee Transparency View để nhân viên tự xem công ty đang theo dõi gì trên máy mình.

3. Đánh giá Tính cạnh tranh (Competitiveness)

    Đối thủ trên thị trường: Microsoft Intune, NinjaOne, Datto RMM, FleetDM (osquery), JumpCloud.

    Vị thế của SentinelLAN:

        So với "ông lớn" (Intune, NinjaOne): SentinelLAN nhẹ hơn, dễ triển khai hơn, không bị rối rắm bởi hàng nghìn policy phức tạp, chi phí gần như bằng 0 nếu tự host.

        So với osquery / FleetDM: FleetDM mạnh về truy vấn SQL trên OS, nhưng SentinelLAN định hướng thành một bộ RMM hoàn chỉnh có giao diện phân quyền thân thiện (Admin/Technician/Employee).

    Bất lợi cạnh tranh: Các phần mềm RMM được doanh nghiệp trả tiền chủ yếu vì 2 tính năng: Cài phần mềm từ xa (Software Deployment) và Chạy script sửa lỗi (Remote Shell/Execution). SentinelLAN cố tình loại bỏ hoàn toàn arbitrary execution (chạy lệnh tùy ý) để bảo mật. Điều này tốt cho an toàn nhưng lại giảm 50% lý do IT Helpdesk muốn mua một tool RMM.

4. Điểm mới & Điểm cũ
Yếu tố	Chi tiết phân tích
ĐIỂM CŨ<br>(Commoditized)	• Thu thập Telemetry cơ bản (CPU, RAM, Disk, Hostname, OS). Hàng trăm tool mã nguồn mở đã làm được.<br>• Mô hình Agent định kỳ Heartbeat về Central Server.<br>• Phân quyền RBAC cơ bản (Admin, Tech, Employee) dùng Cookie/JWT.
ĐIỂM MỚI<br>(Innovations & Nuances)	• Mô hình "Lệnh như giao dịch tài chính": Lệnh không gửi thô mà đóng gói có allow-list, Nonce, thời hạn (Expiry), Server HMAC / chữ ký số, bắt buộc có lý do (Reason) và lưu Audit Append-only chống chối bỏ.<br>• Employee Transparency View: Biến nhân viên từ "đối tượng bị theo dõi" thành một bên giám sát ngược lại tính minh bạch của IT (rất hợp xu thế ESG và đạo đức công nghệ).<br>• Cơ chế chống BOLA (Broken Object Level Authorization): Employee gọi API thiết bị nhưng server bind chặt với OrganizationId + AssignedUserId, không dựa vào ID client gửi lên.
5. Điểm chưa nghĩ đến (Blind Spots & Oversights)

    Vấn đề làm việc từ xa (Remote / Hybrid Work vs Cái tên "SentinelLAN"):

        Cái tên dự án là SentinelLAN, nhưng thế giới hiện nay là làm việc từ xa. Khi nhân viên mang laptop ra quán cà phê, Agent kết nối về Server thế nào?

        Kiến trúc chưa mô tả rõ việc Agent đi qua NAT/Firewall ra ngoài Internet (Reverse Proxy, mTLS, hay Cloudflare Tunnels).

    Bão kết nối khi Agent Reconnect (Thundering Herd Problem):

        Khi cả công ty bật máy vào 8h30 sáng, hoặc sau khi server phục hồi từ sự cố, hàng ngàn Agent cùng lúc xả offline queue và gửi Heartbeat. Nếu không có thuật toán Jitter (độ trễ ngẫu nhiên) đủ tốt, API và PostgreSQL sẽ bị nghẽn (DDoS nội bộ).

    Mâu thuẫn "Tự gỡ cài đặt" (Agent Tampering):

        Trong tài liệu Tuần 5 có ghi: "Xác nhận Agent không cần quyền Administrator cho hành vi MVP".

        Lỗ hổng logic: Nếu Agent không chạy quyền Admin/System, một nhân viên (hoặc malware) có thể dễ dàng mở Task Manager và bấm End Task hoặc xóa thư mục cài đặt của Agent. Hệ thống quản trị thiết bị mà user tự tắt được thì mất tác dụng kiểm soát.

    Cơ chế thu hồi chứng thực khẩn cấp (Emergency Kill-Switch):

        Nếu một máy bị đánh cắp hoặc nhiễm mã độc tống tiền (Ransomware), quản trị viên cần ngắt kết nối lập tức. Chưa thấy thiết kế cơ chế Revoke tức thời thông qua SignalR push event mà vẫn đang dựa nhiều vào chu kỳ Heartbeat tiếp theo.

6. Điểm khuất công nghệ (Hidden Tech Pitfalls & Architectural Traps)

Nếu đội ngũ phát triển không chú ý, hệ thống sẽ vấp phải các bẫy kỹ thuật rất đau đầu sau:
a. Bẫy cách ly Session trên Windows (Session 0 Isolation)

    .NET Agent chạy dưới dạng Windows Service sẽ hoạt động tại Session 0.

    Màn hình người dùng (Desktop, UI, thông báo) chạy ở Session 1, Session 2...

    Hậu quả: Một Windows Service ở Session 0 không thể tương tác trực tiếp với giao diện người dùng (không hiện được popup thông báo "Máy bạn chuẩn bị bị khóa", không thể gọi các hàm khóa màn hình tương tác tiêu chuẩn nếu không giao tiếp IPC qua Named Pipes với một app chạy ngầm ở Taskbar của User).

b. Điểm nghẽn State của SignalR khi scale ngang

    Trong sơ đồ, Redis 8 được đánh dấu là [optional extended profile].

    Rủi ro: Nếu không có Redis Backplane ngay từ đầu, khi backend chạy từ 2 container/server trở lên, một Agent kết nối tới Server A sẽ không nhận được lệnh điều khiển do Web Dashboard gửi vào Server B. Đẩy Redis về "optional" sẽ gây vỡ kiến trúc realtime khi triển khai thực tế.

c. Thực thi "Cách ly mạng" (Network Isolation) trên thực tế

    Dự án đang để lệnh này ở dạng mô phỏng (SimulateNetworkIsolation). Nhưng nếu sau này làm thật:

        Để ngắt mạng mà vẫn giữ liên lạc với SentinelLAN Server để nhận lệnh mở khóa, Agent phải can thiệp vào tầng WFP (Windows Filtering Platform) hoặc cấu hình netsh advfirewall cực kỳ phức tạp.

        Nếu viết sai rule firewall, Agent sẽ tự cô lập chính nó khỏi Server, biến máy con thành một "đảo hoang" vĩnh viễn không thể cứu từ xa.

d. Lưu trữ bí mật trên Windows (DPAPI trap)

    Tuần 5 đề cập đến Windows-protected credential store.

    Cần lưu ý: Nếu dùng DPAPI (ProtectedData.Protect) với cờ DataProtectionScope.LocalMachine, mọi tiến trình chạy trên máy đó đều giải mã được. Nếu dùng CurrentUser, khi chạy dưới user NT AUTHORITY\SYSTEM, profile của SYSTEM có thể bị reset hoặc lỗi trong một số đợt Windows Update lớn, dẫn tới mất DeviceSecret và Agent bị "văng" khỏi hệ thống (phải enroll lại bằng tay).

TỔNG KẾT & LỜI KHUYÊN CHIẾN LƯỢC

    Đánh giá chung: Dự án được thiết kế rất chỉn chu, tư duy bảo mật (Security-by-Design) vượt trội hơn hẳn các đồ án hoặc sản phẩm khởi nghiệp thông thường. Các chi tiết như HttpOnly CSRF, Append-only audit, BOLA check, HMAC nonce cho thấy tư duy backend rất chắc tay.

    Để dự án thực sự có giá trị thương mại/sử dụng thực tế:

        Chấp nhận cho phép Script an toàn: Thay vì cấm tuyệt đối arbitrary execution, hãy cung cấp "Parametric Templates" (Script được Admin phê duyệt trước, ký số, Agent chỉ nhận tham số an toàn) để IT có thể thực hiện các tác vụ sửa lỗi thực tế.

        Tách Agent làm 2 thành phần:

            SentinelLAN.Service (Chạy ngầm dưới quyền LocalSystem để chống tắt trộm, thu thập telemetry phần cứng, xử lý network).

            SentinelLAN.TrayApp (Chạy ở User Session để hiển thị thông báo, phục vụ Employee view và tương tác màn hình).

        Ưu tiên đưa Docker PostgreSQL vào môi trường dev ngay: Đừng để test xanh trên InMemory quá lâu, sự khác biệt về transactional behavior giữa InMemory và Npgsql thực tế trên PostgreSQL sẽ làm gãy các logic audit và concurrency sau này.
        đánh giá hướng đi này cho đồ án tốt nghiệp IT, có nên chuyển hướng, hoặc tinh chỉnh gì
        ĐÁNH GIÁ THẲNG THẮN: TUYỆT ĐỐI KHÔNG NÊN ĐỔI HƯỚNG.

Đề tài này thuộc nhóm "hàng hiếm và điểm cao" trong các đợt bảo vệ đồ án tốt nghiệp (ĐATN) ngành CNTT / An toàn thông tin / Kỹ thuật phần mềm.

Mặt bằng chung ĐATN hiện nay bị bão hòa bởi các web bán hàng, đặt lịch, mạng xã hội, quản lý kho... Hội đồng chấm thi (thầy cô) đã quá ngán ngẩm với các web CRUD đơn giản. Đề tài của bạn kết hợp được Web hiện đại + Windows Internals (System/Service) + Mạng/Bảo mật (Zero Trust, Chữ ký số, Replay Attack). Đây chính xác là "khẩu vị" mà các giảng viên chấm thi đánh giá rất cao.

Tuy nhiên, CẦN PHẢI TINH CHỈNH để biến nó từ một dự án "mang tính kỹ sư làm sản phẩm" thành một "Đồ án tốt nghiệp đạt điểm xuất sắc (A/A+)" theo đúng tiêu chuẩn học thuật.
1. Những "cái bẫy" Hội đồng sẽ hỏi nếu bạn giữ nguyên hiện trạng

Nếu bạn đem nguyên văn bản kế hoạch trên đi bảo vệ, hội đồng thường sẽ xoáy vào 3 điểm yếu chí mạng sau:

    "Tại sao cái gì cũng Simulate (mô phỏng)?"

        Câu hỏi phản biện: "Em nói hệ thống quản trị và cách ly mạng, nhưng code em chỉ Console.WriteLine("Simulated")? Thế thì khác gì một web gửi tin nhắn bình thường?"

        Nếu phần lõi chỉ là mô phỏng, hội đồng sẽ đánh giá hàm lượng kỹ thuật thực tế bị rỗng.

    "Hàm lượng nghiên cứu khoa học / thuật toán ở đâu?"

        ĐATN không chỉ chấm bạn viết bao nhiêu dòng code (Engineering), mà chấm bạn giải quyết bài toán khoa học gì (Computer Science). Nếu chỉ ghép thư viện lại, bạn sẽ bị điểm trung bình.

    "Tại sao không dùng các giải pháp mã nguồn mở có sẵn như osquery / Wazuh?"

        Bạn cần một lý do bảo vệ thuyết phục cho sự tồn tại của SentinelLAN.

2. Kế hoạch tinh chỉnh để "Ăn trọn điểm A+"

Để tối ưu cho buổi bảo vệ, bạn hãy tinh chỉnh dự án theo 3 trụ cột sau:
Tinh chỉnh 1: Biến 1-2 lệnh "Mô phỏng" thành "Hành động thật nhưng cực kỳ trực quan"

Đừng mô phỏng toàn bộ. Hãy làm thật ít nhất 2 hành vi mà hội đồng có thể nhìn thấy ngay trên màn hình máy test trong buổi bảo vệ:

    Hành vi 1: Khóa máy thật (Lock Workstation):

        Thay vì SimulateLock, dùng P/Invoke gọi thẳng Windows API: [DllImport("user32.dll")] public static extern bool LockWorkStation();.

        Hiệu ứng demo: Bấm nút trên Web Dashboard -> 1 giây sau máy laptop demo phụt tắt màn hình vào màn hình Lock Windows. Hội đồng sẽ vỗ tay ngay vì tính trực quan.

    Hành vi 2: Thu thập thông tin phần cứng/bảo mật thật qua WMI/CIM:

        Dùng C# đọc trạng thái bật/tắt của BitLocker hoặc Windows Defender trên máy thật (root\CIMV2). Trực quan hơn nhiều so với chỉ đọc vài con số CPU/RAM thông thường.

Tinh chỉnh 2: Đổi tên đề tài và "Học thuật hóa" cuốn Báo cáo (Report)

Đừng đặt tên đề tài là "Xây dựng phần mềm quản lý thiết bị SentinelLAN" (nghe như đồ án môn học hoặc dự án gia công). Hãy đổi thành:

    "Nghiên cứu và hiện thực hóa hệ thống quản lý thiết bị đầu cuối an toàn (Endpoint Governance) dựa trên nguyên lý Zero Trust và kiểm soát toàn vẹn dữ liệu kiểm toán."

Trong cuốn báo cáo, bạn hãy đưa vào các mô hình học thuật chuẩn quốc tế:

    Mô hình đe dọa (Threat Modeling): Dùng chuẩn STRIDE (Spoofing, Tampering, Repudiation, Information Disclosure, Denial of Service, Elevation of Privilege) để phân tích các rủi ro của hệ thống Endpoint.

    Nguyên lý Zero Trust (NIST SP 800-207): Chứng minh hệ sinh thái của bạn: "Never Trust, Always Verify" (Agent không tin server nếu thiếu chữ ký, Server không tin Agent nếu sai header/token).

    Bài toán Chống chối bỏ (Non-Repudiation): Trình bày kỹ thuật mã hóa: Nonce, HMAC/Asymmetric Signature, Append-only Audit Log.

Tinh chỉnh 3: Nhấn mạnh giá trị "Đạo đức công nghệ & Quyền riêng tư" (Điểm mới)

    Hãy chuẩn bị sẵn một slide so sánh: "Phần mềm giám sát truyền thống (Employee Monitoring Software - Spyware)" VS "Hệ thống quản trị minh bạch (SentinelLAN)".

    Nêu bật tính năng Employee Transparency View. Đây là "đòn bẩy" cực mạnh về mặt xã hội/đạo đức công nghệ, giúp bạn ghi điểm tuyệt đối với các thầy cô khó tính vì đề tài có tính nhân văn (tuân thủ tinh thần Nghị định 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân tại Việt Nam hoặc GDPR).

3. Kịch bản Demo 5 phút "Hạ gục" Hội đồng

Nếu bạn chuẩn bị kịch bản demo chuẩn xác, bạn sẽ làm chủ hoàn toàn buổi bảo vệ:

    Phút 1-2 (Bình thường): Mở 2 màn hình (Web Admin trên máy chiếu, 1 laptop phụ chạy Agent). Thao tác đăng nhập Web, Dashboard nhảy SignalR hiển thị máy phụ Online, thông số CPU/RAM nhảy realtime.

    Phút 3 (Tấn công mô phỏng - Cực kỳ đắt giá): Dùng Postman giả mạo gửi một lệnh can thiệp vào máy nạn nhân:

        Thử gửi sai Nonce -> Server từ chối.

        Thử replay một request cũ -> Hệ thống bắt được Replay Attack.

        Hội đồng sẽ thấy bạn thực sự hiểu sâu về bảo mật chứ không chỉ làm web.

    Phút 4 (Thực thi an toàn): Bấm lệnh Lock Workstation có kèm lý do "Nghi ngờ thiết bị bị truy cập trái phép" -> Máy laptop phụ lập tức bị khóa vào màn hình chờ Windows -> Hệ thống ghi một dòng Audit không thể sửa/xóa.

    Phút 5 (Minh bạch): Đăng nhập vào tài khoản Employee -> Xem màn hình Transparency: Thấy rõ IT vừa làm gì, thu thập những thông số nào, hoàn toàn không bị xem trộm màn hình hay đọc trộm phím gõ.

TỔNG KẾT

Hãy giữ nguyên khung sườn kiến trúc hiện tại của dự án, cắt bớt các mục tiêu xa vời ngoài lề, tập trung hoàn thiện Agent Windows thật (chạy nền) + Web Dashboard + Cơ chế chữ ký/bảo vệ API. Bạn đang nắm trong tay một đề tài rất sáng nước để lấy điểm tối đa.
Hoàn toàn ĐƯỢC, thậm chí đây là một hướng mở rộng cực kỳ thực tế và "ăn điểm" rất mạnh.

Về bản chất, VPS (Oracle Cloud, AWS EC2, Google Cloud Compute) cũng chỉ là các máy chủ ảo (Virtual Machine). Kiến trúc Agent–Server của SentinelLAN không phân biệt máy đó đang đặt ở phòng làm việc hay nằm trên Data Center của Oracle/AWS.

Nếu bạn muốn tận dụng SentinelLAN để làm "Bảng điều khiển tập trung (Single Pane of Glass)" quản lý toàn bộ VPS cá nhân, đây là cách phân tích và những thứ cần điều chỉnh:
1. Tại sao kiến trúc của bạn lại CỰC KỲ HỢP với Cloud VPS?

    Vượt qua mọi rào cản NAT / Firewall của Cloud (Outbound Connection):

        Bình thường muốn quản lý VPS, bạn phải mở Port 22 (SSH) ra ngoài Internet. Việc này dẫn đến nguy cơ bị botnet quét mật khẩu (brute-force) 24/7.

        Với SentinelLAN: Agent trên VPS tự chủ động kết nối ra ngoài (Outbound) về Server SentinelLAN qua HTTPS/SignalR (Port 443). Bạn có thể đóng hoàn toàn port SSH trên Security Group của AWS/Oracle, máy chủ vẫn được giám sát và điều khiển bình thường.

    Gom tất cả nhà cung cấp về một nơi (Multi-Cloud Dashboard):

        Bạn có 2 con VPS Oracle Free Tier, 1 con AWS t2.micro, 1 con GCP. Mỗi bên có một giao diện quản trị riêng rất phiền phức.

        Cài Agent SentinelLAN vào, bạn chỉ cần mở 1 trang web duy nhất là thấy trạng thái cả đàn VPS xanh/đỏ thế nào.

    Mã nguồn .NET hiện tại chạy tốt trên Linux:

        Hầu hết VPS đều chạy Ubuntu/Debian/Alpine. .NET (từ .NET Core đến .NET 8/9/10) chạy trên Linux cực kỳ mượt và tốn rất ít RAM (khoảng 30-50MB RAM cho Agent).

        Thay vì chạy dưới dạng Windows Service, bạn chỉ cần cấu hình để nó chạy dưới dạng Linux Daemon (systemd). Mã nguồn C# xử lý logic lõi (Core) giữ nguyên 100%.

2. Sự khác nhau giữa "Quản lý Máy văn phòng" và "Quản lý Cloud VPS"

Nếu muốn hỗ trợ thêm VPS, bạn cần tinh chỉnh một số chức năng (đặc biệt là Telemetry và Lệnh) cho phù hợp với ngữ cảnh máy chủ:
Đặc tính	Máy văn phòng (Office Endpoint)	Cloud VPS (Server Management)
Hệ điều hành	Chủ yếu là Windows 10/11	90% là Linux (Ubuntu, Debian, AlmaLinux)
Telemetry cần đo	CPU, RAM, Disk, Pin, Trạng thái Khóa màn hình	• Load Average (1m, 5m, 15m)<br>• Băng thông In/Out (Rất quan trọng vì cloud tính tiền bandwidth)<br>• Dung lượng Disk & Inode (Hết inode là crash app)<br>• Trạng thái Docker container / Services
Lệnh an toàn (Commands)	• Khóa máy (LockWorkStation)<br>• Ngắt kết nối mạng mô phỏng	• Khởi động lại Service (VD: systemctl restart nginx)<br>• Khởi động lại Docker container<br>• Xóa file tạm/log rác giải phóng ổ đĩa (journalctl --vacuum-size)<br>• Reboot an toàn
Quyền riêng tư	Cần Employee Transparency View	Không cần (vì là server của bạn, không có nhân viên nào dùng)
3. Cần sửa gì trong Code hiện tại để biến Agent chạy được trên VPS?

Bạn không cần đập đi xây lại, chỉ cần thêm một số nhánh nhỏ:
Bước 1: Cho phép Agent chạy đa nền tảng (Cross-platform Host)

Trong file Program.cs của Worker Agent, thay vì chỉ dùng .UseWindowsService(), bạn viết:
code C#

var builder = Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(); // Chạy trên Windows
}
else if (OperatingSystem.IsLinux())
{
    builder.Services.AddSystemd(); // Chạy trên Ubuntu/Debian của Oracle/AWS
}

(Package NuGet: Microsoft.Extensions.Hosting.Systemd)
Bước 2: Thu thập thông số Linux (Thay thế WMI của Windows)

    Trên Windows bạn đọc WMI, thì trên Linux VPS bạn đọc qua hệ thống file /proc:

        CPU/Load: Đọc file /proc/loadavg hoặc /proc/stat.

        RAM: Đọc file /proc/meminfo.

        Uptime: Đọc /proc/uptime.

    Việc đọc file /proc trên Linux bằng C# diễn ra tức thì, tốn gần như 0% CPU.

Bước 3: Thêm Allow-list commands dành riêng cho Server

Giữ vững triết lý của bạn: Vẫn không cho phép chạy shell tùy ý (No Arbitrary Shell) để tránh bị hack ngược nếu server dashboard bị lộ. Nhưng allow-list trên Linux sẽ là:

    RestartService(string serviceName): Chỉ cho phép các service trong danh sách trắng (docker, nginx, caddy).

    RebootServer(): Thực hiện lệnh khởi động lại VPS một cách an toàn.

4. Giá trị cộng thêm cho Đồ Án Tốt Nghiệp nếu thêm tính năng này

Nếu bạn đưa việc "Quản lý được cả Cloud VPS" vào đồ án, điểm số và độ ấn tượng sẽ tăng vọt:

    Chứng minh năng lực đa nền tảng (Cross-Platform Mastery):

        Bạn có thể tự tin nói với hội đồng: "Hệ thống của em thiết kế theo mô hình Clean Architecture, tầng Core độc lập hoàn toàn với OS. Em đã triển khai thực tế cùng một Agent trên cả laptop Windows 11 và các Cloud VPS chạy Linux Ubuntu trên hạ tầng Oracle Cloud và AWS."

    Kịch bản Demo "Bá đạo":

        Bên cạnh việc demo laptop bị khóa màn hình, bạn mở một tab web đang chạy trên VPS Oracle (ví dụ: trang web Nginx báo lỗi 502 hoặc sập).

        Trên Dashboard SentinelLAN, bạn bấm: Restart Nginx -> Trang web lập tức sống lại.

        Hội đồng sẽ thấy ngay ứng dụng thực tiễn cực lớn: Tự xây dựng một công cụ DevOps/SRE thu nhỏ để quản lý hạ tầng cá nhân mà không cần phụ thuộc vào bên thứ ba.
        

liệu có thể dùng để quản lý vps luôn không ( trong trường hợp cá nhân dùng là tôi remote vps oracle, aws, google..)

Hoàn toàn ĐƯỢC, thậm chí đây là một hướng mở rộng cực kỳ thực tế và "ăn điểm" rất mạnh.

Về bản chất, VPS (Oracle Cloud, AWS EC2, Google Cloud Compute) cũng chỉ là các máy chủ ảo (Virtual Machine). Kiến trúc Agent–Server của SentinelLAN không phân biệt máy đó đang đặt ở phòng làm việc hay nằm trên Data Center của Oracle/AWS.

Nếu bạn muốn tận dụng SentinelLAN để làm "Bảng điều khiển tập trung (Single Pane of Glass)" quản lý toàn bộ VPS cá nhân, đây là cách phân tích và những thứ cần điều chỉnh:
1. Tại sao kiến trúc của bạn lại CỰC KỲ HỢP với Cloud VPS?

    Vượt qua mọi rào cản NAT / Firewall của Cloud (Outbound Connection):

        Bình thường muốn quản lý VPS, bạn phải mở Port 22 (SSH) ra ngoài Internet. Việc này dẫn đến nguy cơ bị botnet quét mật khẩu (brute-force) 24/7.

        Với SentinelLAN: Agent trên VPS tự chủ động kết nối ra ngoài (Outbound) về Server SentinelLAN qua HTTPS/SignalR (Port 443). Bạn có thể đóng hoàn toàn port SSH trên Security Group của AWS/Oracle, máy chủ vẫn được giám sát và điều khiển bình thường.

    Gom tất cả nhà cung cấp về một nơi (Multi-Cloud Dashboard):

        Bạn có 2 con VPS Oracle Free Tier, 1 con AWS t2.micro, 1 con GCP. Mỗi bên có một giao diện quản trị riêng rất phiền phức.

        Cài Agent SentinelLAN vào, bạn chỉ cần mở 1 trang web duy nhất là thấy trạng thái cả đàn VPS xanh/đỏ thế nào.

    Mã nguồn .NET hiện tại chạy tốt trên Linux:

        Hầu hết VPS đều chạy Ubuntu/Debian/Alpine. .NET (từ .NET Core đến .NET 8/9/10) chạy trên Linux cực kỳ mượt và tốn rất ít RAM (khoảng 30-50MB RAM cho Agent).

        Thay vì chạy dưới dạng Windows Service, bạn chỉ cần cấu hình để nó chạy dưới dạng Linux Daemon (systemd). Mã nguồn C# xử lý logic lõi (Core) giữ nguyên 100%.

2. Sự khác nhau giữa "Quản lý Máy văn phòng" và "Quản lý Cloud VPS"

Nếu muốn hỗ trợ thêm VPS, bạn cần tinh chỉnh một số chức năng (đặc biệt là Telemetry và Lệnh) cho phù hợp với ngữ cảnh máy chủ:
Đặc tính	Máy văn phòng (Office Endpoint)	Cloud VPS (Server Management)
Hệ điều hành	Chủ yếu là Windows 10/11	90% là Linux (Ubuntu, Debian, AlmaLinux)
Telemetry cần đo	CPU, RAM, Disk, Pin, Trạng thái Khóa màn hình	• Load Average (1m, 5m, 15m)<br>• Băng thông In/Out (Rất quan trọng vì cloud tính tiền bandwidth)<br>• Dung lượng Disk & Inode (Hết inode là crash app)<br>• Trạng thái Docker container / Services
Lệnh an toàn (Commands)	• Khóa máy (LockWorkStation)<br>• Ngắt kết nối mạng mô phỏng	• Khởi động lại Service (VD: systemctl restart nginx)<br>• Khởi động lại Docker container<br>• Xóa file tạm/log rác giải phóng ổ đĩa (journalctl --vacuum-size)<br>• Reboot an toàn
Quyền riêng tư	Cần Employee Transparency View	Không cần (vì là server của bạn, không có nhân viên nào dùng)
3. Cần sửa gì trong Code hiện tại để biến Agent chạy được trên VPS?

Bạn không cần đập đi xây lại, chỉ cần thêm một số nhánh nhỏ:
Bước 1: Cho phép Agent chạy đa nền tảng (Cross-platform Host)

Trong file Program.cs của Worker Agent, thay vì chỉ dùng .UseWindowsService(), bạn viết:
code C#

var builder = Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(); // Chạy trên Windows
}
else if (OperatingSystem.IsLinux())
{
    builder.Services.AddSystemd(); // Chạy trên Ubuntu/Debian của Oracle/AWS
}

(Package NuGet: Microsoft.Extensions.Hosting.Systemd)
Bước 2: Thu thập thông số Linux (Thay thế WMI của Windows)

    Trên Windows bạn đọc WMI, thì trên Linux VPS bạn đọc qua hệ thống file /proc:

        CPU/Load: Đọc file /proc/loadavg hoặc /proc/stat.

        RAM: Đọc file /proc/meminfo.

        Uptime: Đọc /proc/uptime.

    Việc đọc file /proc trên Linux bằng C# diễn ra tức thì, tốn gần như 0% CPU.

Bước 3: Thêm Allow-list commands dành riêng cho Server

Giữ vững triết lý của bạn: Vẫn không cho phép chạy shell tùy ý (No Arbitrary Shell) để tránh bị hack ngược nếu server dashboard bị lộ. Nhưng allow-list trên Linux sẽ là:

    RestartService(string serviceName): Chỉ cho phép các service trong danh sách trắng (docker, nginx, caddy).

    RebootServer(): Thực hiện lệnh khởi động lại VPS một cách an toàn.

4. Giá trị cộng thêm cho Đồ Án Tốt Nghiệp nếu thêm tính năng này

Nếu bạn đưa việc "Quản lý được cả Cloud VPS" vào đồ án, điểm số và độ ấn tượng sẽ tăng vọt:

    Chứng minh năng lực đa nền tảng (Cross-Platform Mastery):

        Bạn có thể tự tin nói với hội đồng: "Hệ thống của em thiết kế theo mô hình Clean Architecture, tầng Core độc lập hoàn toàn với OS. Em đã triển khai thực tế cùng một Agent trên cả laptop Windows 11 và các Cloud VPS chạy Linux Ubuntu trên hạ tầng Oracle Cloud và AWS."

    Kịch bản Demo "Bá đạo":

        Bên cạnh việc demo laptop bị khóa màn hình, bạn mở một tab web đang chạy trên VPS Oracle (ví dụ: trang web Nginx báo lỗi 502 hoặc sập).

        Trên Dashboard SentinelLAN, bạn bấm: Restart Nginx -> Trang web lập tức sống lại.

        Hội đồng sẽ thấy ngay ứng dụng thực tiễn cực lớn: Tự xây dựng một công cụ DevOps/SRE thu nhỏ để quản lý hạ tầng cá nhân mà không cần phụ thuộc vào bên thứ ba.

Model 1:50 AM
Thinking Thoughts

Expand to view model thoughts
chevron_right

Dưới đây là phân tích chi tiết về mặt kiến trúc hệ thống, an ninh mạng và giải pháp công nghệ cho 3 vấn đề bạn vừa nêu:
1. Backend trung tâm: Nên đặt tại Máy chủ công ty (On-Premise) hay VPS ngoài (Cloud)?

Với kiến trúc của SentinelLAN, đặt Backend trên VPS ngoài (Cloud) là phương án tối ưu nhất, đặc biệt là trong bối cảnh làm việc hiện đại:
Tiêu chí	Máy chủ nội bộ công ty (On-Premise)	VPS ngoài (AWS, Oracle Cloud, DigitalOcean...)
Khả năng quản lý	Chỉ quản lý được máy đang cắm dây mạng/WiFi trong công ty. Nếu nhân viên mang máy về nhà hay ra quán café thì mất kết nối (trừ khi công ty dựng VPN rất tốn kém).	Quản lý được mọi lúc, mọi nơi (ở công ty, ở nhà, hay trên các Cloud VPS khác).
Địa chỉ IP & Hạ tầng	Thường là IP động, phụ thuộc vào đường truyền mạng của nhà mạng (FPT, Viettel), dễ sập khi mất điện/mất mạng nội bộ.	IP tĩnh (Static IP), mạng Data Center đạt chuẩn 99.9% Uptime, có backup nguồn điện 24/7.
Cơ chế kết nối (Bảo mật mạng)	Phải mở cổng (Port Forwarding) trên Router công ty -> Rất nguy hiểm vì dễ làm lộ toàn bộ mạng LAN nội bộ.	Rất an toàn: Máy tính của nhân viên chỉ gọi kết nối ra ngoài (Outbound - Port 443 HTTPS) tới VPS. Tường lửa công ty không cần mở bất kỳ cổng Inbound nào.

👉 Khuyến nghị: Dùng một con VPS Cloud (ví dụ: gói miễn phí trọn đời của Oracle Cloud hoặc VPS $5/tháng) để làm máy chủ trung tâm.
2. Nếu bị tấn công qua đường mạng, phòng vệ như thế nào?

Vì Backend đặt trên Internet, nó sẽ đối mặt với các nguy cơ bị scan bot, DDoS và dò mật khẩu. Bạn có thể xây dựng mô hình phòng thủ 4 lớp (Defense-in-Depth) cực kỳ bài bản sau:
code Code

[ Hacker / Internet ] 
        │
        ▼
[ Lớp 1: Cloudflare / Reverse Proxy ] ──> Chặn DDoS, giấu IP gốc của VPS
        │
        ▼
[ Lớp 2: TLS 1.3 + Certificate Pinning ] ──> Chặn nghe lén (MITM)
        │
        ▼
[ Lớp 3: ASP.NET Core Rate Limit + HMAC Nonce ] ──> Chặn Spam, dò quét & Replay Attack
        │
        ▼
[ Lớp 4: Zero Trust Network Access (ZTNA) ] ──> Dashboard Admin chỉ mở cho IP được cấp phép

    Chống DDoS và giấu IP máy chủ (Tầng 1):

        Đặt toàn bộ Backend sau Cloudflare (miễn phí). Cloudflare sẽ lọc sạch các đợt tấn công DDoS tầng 3, 4 và HTTP Flood (tầng 7). Hacker chỉ thấy IP của Cloudflare, không bao giờ biết IP thật của VPS bạn.

    Chống nghe lén và làm giả máy chủ (MITM - Tầng 2):

        Sử dụng chứng chỉ SSL/TLS chuẩn.

        Trên Agent (.NET Windows/Linux), bạn cài đặt cơ chế Certificate Pinning (Ghim chứng chỉ): Agent chỉ chấp nhận nói chuyện với Server nếu mã băm (Hash Fingerprint) của chứng chỉ đúng y hệt mã bạn nhúng sẵn trong Agent. Dù hacker dùng proxy (như Burp Suite, Fiddler) cũng không thể chen vào giữa để trộm dữ liệu.

    Chống giả mạo thiết bị & Replay Attack (Tầng 3):

        Như kế hoạch Tuần 3 của bạn: Mỗi gói tin Agent gửi đi đều kèm Nonce (số dùng 1 lần) + Timestamp (trong vòng 30 giây) + Chữ ký HMAC. Nếu hacker bắt trộm được gói tin và phát lại -> Server thấy trùng Nonce hoặc lệch giờ sẽ vứt bỏ ngay.

        Kích hoạt Rate Limiting trong ASP.NET Core: 1 IP/Device chỉ được gửi tối đa X request/phút.

    Bảo vệ trang Admin Dashboard (Tầng 4):

        Đường dẫn API cho Agent thì mở public, nhưng riêng trang Web Dashboard quản trị của Admin có thể khóa lại bằng Cloudflare Zero Trust (chỉ ai có email của công ty và nhập mã OTP gửi về điện thoại mới vào được trang đăng nhập).

3. Tính năng Remote Desktop: Popup đồng ý 2 phía & Lựa chọn công nghệ

Ý tưởng "Popup cần sự đồng ý từ 2 phía (Two-way Consent)" là một ý tưởng xứng đáng điểm 10 trong mắt hội đồng bảo vệ đồ án. Nó biến phần mềm của bạn thành một công cụ văn minh, bảo vệ quyền riêng tư thay vì bị coi là "phần mềm gián điệp cài lén".
a. Có nên dùng UltraViewer không?

    KHÔNG NÊN. UltraViewer là phần mềm thương mại đóng mã nguồn (Closed-source). Họ không cung cấp API miễn phí để hệ thống của bạn tự động bật/tắt, truyền tham số, hoặc nhúng vào web. Rất khó tích hợp mượt mà vào một đồ án công nghệ.

b. Lựa chọn hoàn hảo nhất: Dự án mã nguồn mở RUSTDESK (Tự Host)

Hiện nay, giải pháp tối ưu số 1 thế giới để thay thế TeamViewer/UltraViewer là RustDesk:

    Mã nguồn mở (Open-source): Bạn có thể tự dựng máy chủ điều khiển (Relay Server - hbbr và Rendezvous Server - hbbs) ngay trên chính con VPS của bạn bằng Docker. Hoàn toàn miễn phí, tốc độ nội địa siêu nhanh và dữ liệu màn hình không chạy qua server của bên thứ ba nào.

    Hỗ trợ Command-line (CLI): Cho phép phần mềm khác ra lệnh khởi động, cấu hình IP server từ xa qua dòng lệnh.

c. Luồng hoạt động của tính năng Remote với Popup đồng ý 2 phía:
code Code

[ Admin Web Dashboard ]                 [ SentinelLAN Agent ]                  [ Màn hình Nhân viên ]
         │                                       │                                       │
         ├────── 1. Bấm "Yêu cầu Remote" ───────>│                                       │
         │       (Kèm lý do: Sửa lỗi Outlook)    ├────── 2. Hiện Popup thông báo ───────>│
         │                                       │                                       │
         │                                       │<───── 3. Bấm [ĐỒNG Ý] (Accept) ───────┤
         │                                       │                                       │
         │                                       ├────── 4. Khởi chạy RustDesk ngầm      │
         │                                       │       với mật khẩu dùng 1 lần         │
         │<───── 5. Gửi ID & Pass 1 lần ─────────┤                                       │
         │                                       │                                       │
[ Admin mở Web/App RustDesk ] ───────────────────┴───────────────────────────────────────► [ Bắt đầu phiên Remote ]

    Bước 1: Kỹ thuật viên (Admin) trên Web chọn máy và bấm nút "Yêu cầu trợ giúp từ xa", nhập lý do: "Hỗ trợ cài đặt phần mềm Office".

    Bước 2 (Popup): Server bắn lệnh qua SignalR xuống Agent. Agent (thông qua ứng dụng khay hệ thống Tray App ở màn hình người dùng) hiện một hộp thoại nổi:

        "Kỹ thuật viên [Nguyễn Văn A] yêu cầu kết nối vào màn hình của bạn để: [Hỗ trợ cài đặt phần mềm Office]. Bạn có đồng ý không?"

    Bước 3:

        Nếu người dùng bấm Từ chối (hoặc sau 60 giây không bấm): Báo về Dashboard "Phiên kết nối bị hủy bởi người dùng".

        Nếu người dùng bấm Đồng ý:

    Bước 4: Agent sinh ra một mật khẩu ngẫu nhiên dùng 1 lần (OTP 6 số), tự động bật tiến trình RustDesk trỏ về IP server VPS của bạn.

    Bước 5: Agent gửi ID và mật khẩu đó về Dashboard cho Admin. Admin chỉ cần click mở remote là kết nối được ngay.

    Bước 6: Khi kết thúc phiên, mật khẩu bị hủy bỏ lập tức, ghi nhận vào Audit Log: "Ai remote vào máy ai, lúc mấy giờ, lý do là gì, người dùng đã chấp thuận".

Tổng kết đánh giá

    Kiến trúc: Dùng VPS ngoài làm Relay Server là nước đi chuẩn nhất cho thời đại làm việc linh hoạt.

    Bảo mật: Dùng Cloudflare + Certificate Pinning + HMAC Token sẽ tạo ra một hệ thống phòng thủ kiên cố, hoàn toàn đủ tiêu chuẩn trả lời phỏng vấn bảo mật trước hội đồng.

    Remote Control: Tích hợp RustDesk Self-hosted kết hợp cơ chế xin phép 2 chiều sẽ là tính năng "sát thủ" (killer feature) giúp đồ án của bạn vượt trội hoàn toàn so với các sinh viên khác!

Model 1:53 AM
Thinking Thoughts

Expand to view model thoughts
chevron_right

Dưới đây là thiết kế chi tiết về cấu trúc triển khai chính thức (Production), luồng kết nối, cơ chế chống nhảy nhầm công ty (Multi-tenancy) và kịch bản xử lý khi máy bị rút cáp mạng.
1. Cấu trúc hệ thống khi Build chính thức (Production Topology)

Hệ thống được đóng gói và chia làm 2 cụm độc lập: Cụm Cloud (Trung tâm) và Cụm Endpoint (Máy nhân viên).
code Code

[ INTERNET / CLOUDFLARE ]
                               │ (HTTPS/WSS Port 443)
┌──────────────────────────────▼────────────────────────────────┐
│  CLOUDER SERVER / VPS (Ubuntu Docker)                         │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │ Reverse Proxy (Caddy / Nginx) - Tự cấp SSL Let's Encrypt│  │
│  └──────────────┬──────────────────────────┬───────────────┘  │
│                 │ :3000                    │ :8080            │
│  ┌──────────────▼──────────┐ ┌─────────────▼───────────────┐  │
│  │ Web Admin (Next.js 16)  │ │ API & SignalR (.NET 10)     │  │
│  └─────────────────────────┘ └─────────────┬───────────────┘  │
│                                            │                  │
│       ┌────────────────────────────────────┼──────────────┐   │
│       │                                    │              │   │
│  ┌────▼──────────┐                  ┌──────▼─────┐ ┌──────▼─┐ │
│  │ PostgreSQL 17 │                  │ Redis 8    │ │RustDesk│ │
│  │ (Dữ liệu gốc) │                  │ (Pub/Sub)  │ │ Relay  │ │
│  └───────────────┘                  └────────────┘ └────────┘ │
└───────────────────────────────────────────────────────────────┘
                                ▲
                                │ Kết nối ngược ra ngoài (Outbound Only)
┌───────────────────────────────┴───────────────────────────────┐
│  MÁY TÍNH NHÂN VIÊN (Client Endpoint)                        │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │ SentinelLAN.Worker (Windows Service - Quyền SYSTEM)     │  │
│  │ • Chạy ngầm 24/7, đo CPU/RAM, kiểm soát lệnh            │  │
│  │ • Lưu DB cục bộ (SQLite/LiteDB) khi mất mạng           │  │
│  └──────────────────────────┬──────────────────────────────┘  │
│                             │ IPC (Named Pipes)               │
│  ┌──────────────────────────▼──────────────────────────────┐  │
│  │ SentinelLAN.TrayApp (Chạy trong Session người dùng)     │  │
│  │ • Biểu tượng góc Taskbar, hiện Popup xin quyền Remote   │  │
│  │ • Màn hình minh bạch Transparency View                  │  │
│  └─────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘

2. Làm sao Agent kết nối được với Admin? (Hub & Spoke Pattern)

Nguyên tắc vàng: Admin KHÔNG BAO GIỜ kết nối trực tiếp vào địa chỉ IP của Agent (vì máy nhân viên nằm sau tường lửa, NAT, IP động, đổi mạng liên tục). Mọi liên lạc đều đi qua Hub trung tâm (Server VPS) bằng giao thức WebSockets / SignalR.

    Bước 1 (Agent khởi động):

        Service của Agent bật lên -> Đọc DeviceId và DeviceSecret đã được mã hóa trong máy.

        Gọi kết nối ra ngoài (Outbound) tới VPS: wss://api.yourdomain.com/hubs/device.

        Server xác thực hợp lệ -> Cho phép duy trì kết nối WebSocket thường trực.

    Bước 2 (Admin vào Dashboard):

        Admin mở trình duyệt Web -> Đăng nhập thành công -> Trình duyệt mở WebSocket: wss://api.yourdomain.com/hubs/admin.

    Bước 3 (Giao tiếp thời gian thực):

        Khi Agent có thông số CPU/RAM mới -> Đẩy lên Hub -> Hub bắn sang kênh của Admin.

        Khi Admin bấm nút "Lock" -> Lệnh đẩy xuống Hub -> Hub tìm đúng kết nối WebSocket của Agent đó và bắn lệnh xuống.

        Toàn bộ quá trình phản hồi mất chưa tới 0.1 giây, không cần mở port mạng của nhân viên.

3. Có bị "nhảy nhầm" công ty không? (Chống rò rỉ dữ liệu Tenant)

Đây là bài toán Multi-Tenancy (Đa người thuê). Trong một hệ thống RMM/MDM chuẩn, hiện tượng "nhảy nhầm" máy của công ty này sang công ty khác được ngăn chặn bằng 3 lớp rào chắn bắt buộc:
Lớp 1: Khóa chặt từ lúc cài đặt (Enrollment Binding)

    Khi một công ty tạo tài khoản, họ được cấp một EnrollmentToken riêng biệt (ví dụ: gắn chết với Org_ABC).

    Khi IT mang bộ cài cài lên máy nhân viên, mã token này được kích hoạt. Server sẽ tạo ra một bản ghi trong Database:

        DeviceId: DEV_123

        OrganizationId: Org_ABC (Cố định vĩnh viễn, không thể tự sửa từ phía client).

        DeviceSecret: Chuỗi ngẫu nhiên 64 ký tự (chỉ Agent và Server biết).

Lớp 2: Bộ lọc tự động ở tầng Cơ sở dữ liệu (EF Core Global Query Filters)

Trong mã nguồn backend ASP.NET Core, bạn dùng tính năng Global Query Filter. Bất kỳ câu truy vấn nào gọi tới bảng Devices, Audits, Alerts đều bị ép buộc phải lọc theo OrganizationId của người đang đăng nhập:
code C#

// Trong AppDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Bất kỳ truy vấn nào cũng tự động thêm: WHERE OrganizationId = @currentTenant
    modelBuilder.Entity<Device>().HasQueryFilter(d => d.OrganizationId == _currentTenantService.OrganizationId);
}

Dù cho lập trình viên có lỡ viết thiếu lệnh WHERE, Database vẫn chặn đứng, Admin Công ty A về mặt toán học không thể nào đọc được bản ghi của Công ty B.
Lớp 3: Cô lập kênh truyền SignalR (Room / Group Isolation)

    Khi Admin Công ty A kết nối SignalR, họ chỉ được join vào nhóm: Group("Tenant_CompanyA").

    Agent của Công ty A chỉ được gán vào Group("Tenant_CompanyA_DeviceX").

    Server tuyệt đối không dùng Clients.All.Send(...), mà chỉ gửi tới đích danh Group của Tenant đó.

4. Khi máy nhân viên bị rút dây cáp mạng (Mất mạng), xử lý sao?

Trường hợp mất mạng (vô tình hoặc cố ý rút cáp để trốn kiểm soát) được hệ thống giải quyết ở cả 2 đầu:
code Code

[Máy nhân viên bị rút cáp] 
       │
       ├────► [Phía Server & Admin] 
       │      • Hết hạn nhịp tim (Heartbeat Timeout > 90s)
       │      • Chuyển trạng thái máy: [CHẤM ĐỎ - OFFLINE]
       │      • Bắn Alert cảnh báo lên Dashboard Admin
       │
       └────► [Phía Agent máy nhân viên]
              • Bật "Chế độ sinh tồn Offline" (Offline Survival Mode)
              • Ghi toàn bộ Log/Audit vào DB cục bộ mã hóa (SQLite)
              • Kích hoạt cơ chế Reconnect (Exponential Backoff)
              • (Tùy chọn) Kích hoạt Offline Policy (Tự động khóa máy nếu mất mạng quá X phút)

A. Phía Server: Phát hiện mất mạng (Heartbeat & Timeout)

    Bình thường, mỗi 30 giây Agent phải gửi 1 tín hiệu Heartbeat ("Tôi vẫn sống").

    Nếu rút cáp, kết nối TCP bị đứt đột ngột (Server chưa kịp nhận gói FIN/RST).

    Server có một Background Worker chạy ngầm: Quét kiểm tra cột LastSeenAtUtc.

    Nếu Thời gian hiện tại - LastSeenAtUtc > 90 giây:

        Tự động cập nhật Database: Status = DeviceStatus.Offline.

        Bắn một thông báo Realtime lên Dashboard của Admin: Biểu tượng máy tính chuyển từ Xanh lá sang Đỏ.

        Ghi vào Audit: "Thiết bị [Laptop_NV01] mất kết nối lúc 14:02:10".

B. Phía Agent: Chế độ "Sinh tồn Offline" (Offline Queue)

    Khi rút mạng, Agent không được phép crash. Nó phát hiện mất mạng (NetworkChange.NetworkAvailabilityChanged).

    Lưu trữ dữ liệu ngoại tuyến: Toàn bộ các thay đổi, log sự kiện, cảnh báo trong máy sẽ được Agent lưu tạm vào một file Database SQLite cục bộ (được mã hóa bằng Windows DPAPI).

    Cơ chế chống nghẽn khi cắm cáp lại (Exponential Backoff + Jitter):

        Agent sẽ không liên tục bắn request để tránh quá tải. Nó thử lại theo cấp số nhân: 2s -> 4s -> 8s -> 16s -> tối đa 60s/lần + một khoảng thời gian trễ ngẫu nhiên (Jitter).

        Khi cắm lại cáp mạng: Agent kết nối thành công, lập tức xả hàng đợi (Flush Queue), gửi bù toàn bộ dữ liệu đo được trong thời gian mất mạng lên Server kèm theo Idempotency-Key (để Server không bị lưu trùng bản ghi).

C. Chống gian lận (Anti-tamper / Fallback Policy)

Nếu công ty có chính sách khắt khe: "Nhân viên rút cáp mạng để cắm USB trộm dữ liệu":

    Bạn có thể cài đặt Chính sách ngoại tuyến (Offline Grace-period) trong Agent:

        Nếu mất kết nối với Server quá 15 phút liên tục, Agent sẽ tự động gọi lệnh khóa màn hình cục bộ và yêu cầu nhập mã Master Password của IT mới cho mở lại.

        Điều này ngăn chặn tuyệt đối ý đồ rút dây mạng để qua mặt hệ thống giám sát.