# Tiến độ thực hiện kế hoạch SentinelLAN

Ngày: 05/10/2026 (GMT+7). Lịch 07:35 đã xóa; thực hiện trực tiếp trong chat. Chưa hoàn thành toàn bộ kế hoạch và chưa có nghiệm thu release.

## Thay đổi trong phiên này

- G1: cấu hình SelfHost, không khởi tạo/đăng ký/mở API Platform/VPS; Compose LAN riêng và hướng dẫn DNS/TLS. Sửa bootstrap bị bỏ qua khi database chỉ có tổ chức Platform.
- G2: token opaque trong SelfHost, bộ cài công ty ghim HTTPS origin và public key; không dùng SL1 để chọn server. Proof đăng ký lưu trước HTTP; retry/restart phục hồi cùng credential/device khi response bị mất, không tạo thêm máy. Migration EnrollmentResponseRecovery thêm EnrolledDeviceId; không sửa migration đã có.
- G3: RSA-PSS/SHA256 tối thiểu 3072 bit; envelope version/key ID và mọi trường nghiệp vụ được ký. SelfHost yêu cầu khóa riêng server; Agent chỉ giữ public key. RSA verifier không fallback HMAC. Quy trình IT luân chuyển key đã ghi; chưa có keyring cập nhật tự động.
- Trong đối chiếu G0/F10/F14/F15: state machine Domain, API chặn bỏ qua bước xử lý/OTP và phê duyệt lại; UI chỉ đưa lựa chọn phù hợp. Reconcile không đưa request đã đóng về chờ nhân viên.

## Bằng chứng đã chạy

- `dotnet test SentinelLAN.slnx --no-restore`: 309/309 (Architecture 1, Domain 23, Application 87, Agent 75, Integration 123). Integration của lượt toàn solution sử dụng InMemory.
- PostgreSQL 18.1 tạm, riêng trên VPS qua SSH tunnel: DeviceConcurrencyTests + ActivationAndQrIntegrationTests + SelfServiceIntegrationTests đạt 14/14; EnrollmentRecoveryTests + DeviceConcurrencyTests đạt 7/7. Hai lượt có test trùng nhau, không cộng số để tuyên bố coverage.
- Migration từ database mới và migration recovery trên database test đã có được áp dụng trong các lượt PostgreSQL.
- Sau thay đổi lifecycle cuối, SelfServiceIntegrationTests chạy lại trên PostgreSQL đạt 6/6. Container test có label riêng đã dừng/xóa cùng dữ liệu tạm; SSH tunnel đã đóng.
- Web: 36/36 Vitest; typecheck và lint đạt; build cả Company, Employee, Platform đạt.
- Agent Worker và Setup Windows build đạt; Compose LAN `config --quiet` đạt với cấu hình giả riêng; cú pháp script cài/build MSI đạt.
- Format verification cho API và RSA/attempt adapters đạt (có cảnh báo workspace load). NuGet báo không đọc được nguồn advisory; chưa xác minh sạch dependency.
- Chưa build MSI release công ty với origin/public key thật, chưa cài Windows sạch và chưa triển khai profile SelfHost lên server LAN thật. Không dùng build hay test giả làm bằng chứng thao tác OS thật.

## Backlog F01–F24

Các dòng dưới là việc còn phải nghiệm thu/hoàn thiện, không phải danh sách tính năng đã đạt.

| Luồng | Bằng chứng hiện có / việc làm phiên này | Còn lại |
|---|---|---|
| F01 bootstrap | SelfHostDeploymentTests, BootstrapOrganizationTests, Compose LAN | DNS/TLS, bootstrap Production từ đầu, hai laptop và mất Internet |
| F02 phiên | AuthenticationApiTests hiện có đạt trong suite | Smoke HTTPS theo vai trò trên LAN, recovery Admin |
| F03 người dùng | UserManagement hiện có, test suite đạt | Rà soát ma trận API và khôi phục Admin cuối; quyền/đồng thời đổi vai trò |
| F04 token | Opaque SelfHost, test single-use và tranh chấp PostgreSQL | Danh sách/thu hồi token và UX, đối chiếu quyền cho toàn bộ vòng đời |
| F05 bộ cài | CompanyEnrollment, Setup build, proof recovery + migration/test | MSI/EXE công ty thật, Windows sạch, upgrade/reboot/cài lại |
| F06 giao máy | DeviceLifecycleIntegrationTests hiện có | Assignment đồng thời trên PostgreSQL, hồ sơ tài sản/người sử dụng khi đổi máy |
| F07 Agent | 75 test Agent; protected recovery store, queue/nonce hiện có | Đo CPU/RAM, sleep/wake/Wi-Fi/reboot, liên tục 24h |
| F08 giám sát | UI build; API suite hiện có | Hai máy thật, số liệu chính xác, trạng thái dữ liệu cũ và mọi lỗi UI |
| F09 báo lỗi | SelfServiceIntegrationTests, UI trạng thái phù hợp | Một cửa hỗ trợ thay các cửa incident trùng; thử người dùng |
| F10 xử lý | State machine/anti-repeat approval và Domain/API/UI tests | Nhận ticket đồng thời, liên kết work order/incident, toàn vòng đời từ hai vai trò thật |
| F11 ảnh | Test ảnh do nhân viên chọn và quyền hiện có | Cross-object tải ảnh, orphan/retention và thử upload thật |
| F12 lệnh | RSA liên thông, tamper/replay/wrong-device/expiry tests | Key rotation thực tế, receipt/OS verification và IPC/least privilege trên lab |
| F13 cảnh báo | Online/offline và test hiện có | Ngưỡng tài nguyên có thời gian duy trì, chống lặp/flapping và hết cảnh báo |
| F14 phần mềm | Catalog snapshot/signature tests; chặn đóng trước kết quả | Đường IT thủ công có bằng chứng, SHA/publisher/reboot và cài thật trên lab |
| F15 bảo trì/gỡ | OTP single-use PostgreSQL; chặn bỏ qua OTP | Xác nhận tại máy, tự hết hạn và gỡ MSI thật trong lab |
| F16 thu hồi | Credential tests; proof recovery từ chối credential revoked | Thu hồi offline/reinstall/đổi người và giữ liên kết lịch sử tài sản |
| F17 backup | Script backup hiện có, chưa nghiệm thu phiên này | DB/config/attachment/keys mã hóa; restore độc lập, RPO/RTO đo thật |
| F18 nâng cấp | Migration recovery, Agent/Setup build | Checksum release, backup trước migration, upgrade/rollback MSI/server |
| F19 policy | Policy tests hiện có | Version/rollback/conflict/capability Windows thật |
| F20 tài sản | ITAM/CMMS code/test hiện có | Loan concurrency, bàn giao/thu hồi/tài sản đang có việc mở |
| F21 thông báo/lịch | SelfService thông báo test PostgreSQL | Trùng lịch, đổi/hủy/hết hạn, offline realtime và kênh LAN |
| F22 QR | ActivationAndQr tests PostgreSQL | Giả/thu hồi/retired/cross-object; camera/quét trên thiết bị thật |
| F23 báo cáo | Chưa nghiệm thu phiên này | Phạm vi ngày/quyền/export CSV injection, dữ liệu rỗng/lớn |
| F24 Panic | Allow-list/lab boundary hiện có; test UI không hứa cô lập | Anti-spam, hướng dẫn/thông báo thực tế; thao tác thật chỉ trên thiết bị lab opt-in |

## Điều kiện lab đang thiếu

Chưa có thông tin truy cập laptop Windows thứ hai và lựa chọn máy/VM làm server LAN. Đã hỏi trong chat; chưa có trả lời tại lúc lập tài liệu. Không tự dùng VPS đang chạy production để thay chứng minh LAN hoạt động khi mất Internet. Database PostgreSQL thử được tạo riêng, bind loopback, giới hạn tài nguyên và được dọn sau kiểm thử.

Giữ nguyên các thay đổi đã có trước phiên; chưa commit/deploy toàn bộ working tree. G1/G2/G3 vẫn chưa được đánh dấu qua cổng nghiệm thu vì các điều kiện Windows/LAN thật chưa đạt. G4–G9 tiếp tục theo cổng nghiệm thu của kế hoạch gốc; không tuyên bố toàn bộ P0 hoàn thành.
