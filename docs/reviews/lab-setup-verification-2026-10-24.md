# Kiểm chứng lab Agent và mobile — 24/10/2026

Phạm vi: cài và cấu hình Android Studio/emulator, Hyper-V Windows lab theo lựa chọn của người dùng; sửa các lỗi thực tế phát hiện khi chạy. Chưa triển khai lên môi trường production.

## Môi trường trên máy

- Lab riêng: `C:\Users\admin\SentinelLANLab`; hướng dẫn mở bằng shortcut Desktop nằm tại `README-LAB.md` trong thư mục đó.
- Android Studio chính thức 2026.2.1.8, SDK Google và AVD `SentinelLAN_API36`: Android 16/API 36, Google APIs x86_64, Pixel 7, 3 GB RAM, 2 CPU. WHPX báo khả dụng; ADB xác nhận khởi động hoàn tất.
- Không thay Java hệ thống: IDE dùng JBR đi kèm, native build dùng Temurin JDK 17 riêng đã đối chiếu checksum chính thức. Các biến Android được cấu hình cho tài khoản Windows hiện tại.
- Hyper-V VM `SentinelLAN-Agent-Lab`: Generation 2, 2 CPU, 4 GB RAM, VHDX 80 GB dạng dynamic; Secure Boot và vTPM bật, mạng NAT qua Default Switch.
- Windows 11 Enterprise Evaluation từ [Microsoft Evaluation Center](https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise), không dùng VMware/crack. Guest `SLAN-LAB-01`, OS 10.0.26300, `IMAGE_STATE_COMPLETE`; PowerShell Direct xác thực thành công. Đã tạo checkpoint Windows sạch và checkpoint lab sẵn sàng trước enrollment.
- API Release riêng của lab dùng Development/InMemory, lắng nghe trên loopback và địa chỉ NAT của Hyper-V. Android gọi `http://10.0.2.2:5000`. Guest gọi `https://sentinellan-lab.test:5443/health/ready` thành công bằng kiểm tra TLS chuẩn, chứng chỉ lab chỉ tin cậy trong guest. Firewall giới hạn interface NAT, địa chỉ host và đúng IP guest, không mở ra toàn LAN.
- File cài Android Studio và command-line tools được đối chiếu SHA256 chính thức. ISO Windows đã ghi SHA256 cục bộ và kiểm tra chữ ký Microsoft của `setup.exe`; không coi hash tự tính là đối chiếu hash do nhà phát hành công bố.
- Mật khẩu VM/chứng chỉ được lưu bằng Windows DPAPI ngoài repository. Không in mật khẩu vào báo cáo hoặc đưa certificate, image, VHD, cache, log vào Git.

## Lỗi được phát hiện và sửa

### Thông báo trùng trong cùng batch

Một thiết bị offline được giao cho Admin có thể gửi thông báo tới cùng người dùng qua cả nhánh chủ thiết bị và nhánh IT trước `SaveChanges`. Truy vấn chỉ đọc database bỏ sót entity đang được EF theo dõi, tạo trùng khóa deduplication; lần reconcile sau ném `Sequence contains more than one element` trong InMemory, còn database quan hệ có thể từ chối unique constraint.

`SelfServiceStore.FindNotificationByKeyAsync` kiểm tra cả notification đang được theo dõi, giữ đủ điều kiện organization/user/key. Regression thêm thiết bị offline giao cho Admin, chạy reconcile hai lần và xác nhận mỗi người dùng chỉ có một notification cho khóa đó. Test đã tái hiện lỗi trước sửa, sau sửa đạt.

### Module push làm Expo Go lỗi trước đăng nhập

Import `expo-notifications` ở cấp module trong auth/push observer ném lỗi thiếu `ExpoTopicSubscriptionModule` khi chạy Expo Go. Runtime adapter mới chỉ nạp module trong môi trường hỗ trợ; Expo Go vẫn mở được đăng nhập và hộp thông báo trong app. Bật push trong Expo Go trả hướng dẫn dùng bản SentinelLAN do IT cung cấp. Cơ chế quyền, đăng ký, thu hồi, điều hướng và xóa response của bản native được giữ nguyên.

Regression mô phỏng module native ném lỗi khi import và xác nhận auth/observer vẫn tải được trong Expo Go. Toàn bộ suite mobile, bao gồm các kiểm tra ranh giới người dùng của push, đạt.

Expo SDK 57.0.31 yêu cầu React Native 0.86.3, còn dự án trước vòng này pin 0.87.1. Đã đồng bộ RN 0.86.3, safe-area-context 5.7.0, screens 4.26.x và bỏ workaround Metro/Jest của RN 0.87. CI thêm `check:sdk`; chỉ React và TypeScript còn exclude, native dependency nêu trên được kiểm tra online. React 19.2.8 giữ bản vá đã kiểm chứng.

Lần mở lại APK phát hiện SIGSEGV trong `MountingCoordinator::pullTransaction`: dependency gián tiếp đã chọn Reanimated 4.7.1/Worklets 0.13.0, vượt bundled metadata của Expo. Đã khai báo/cố định Reanimated 4.5.1 và Worklets 0.10.1, build/cài lại và kiểm tra cold start. APK `com.sentinellan.employee` đăng nhập, tải đúng máy VM và CPU/RAM/ổ đĩa; sau force-stop và mở lại vẫn khôi phục phiên vào Tổng quan.

Build native dùng Gradle 9.3.1, NDK 27.1.12297006, CMake 3.22.1 và Ninja 1.13.2 chính thức có đối chiếu digest. Ninja 1.10.2 đi kèm SDK không xử lý được đường dẫn dài; bản cũ được giữ backup trong SDK lab. Cấu hình object path CMake chỉ nằm trong project native sinh ngoài repository. [CMake](https://cmake.org/cmake/help/latest/variable/CMAKE_OBJECT_PATH_MAX.html), [Ninja](https://github.com/ninja-build/ninja/releases/tag/v1.12.0).

### Advisory Zod mới trong lượt kiểm tra cuối

Feed audit ghi nhận [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5): collection vượt `.max()`/kích thước cố định vẫn bị duyệt từng phần tử, tạo nhiều lỗi và tốn bộ nhớ. Test trước sửa xác nhận vẫn duyệt phần tử dù đã vượt giới hạn.

Backport nguyên tắc kiểm tra size trước duyệt từ [upstream a0ebf97](https://github.com/colinhacks/zod/commit/a0ebf97) vào Zod 3.25.76 để giữ tương thích Expo/tooling. Vá CJS/ESM của v3 và core v4 đi kèm, gồm array/set và size check của map v4; giữ thứ tự overwrite, input trong giới hạn và lỗi sai kiểu. Manifest khóa version và checksum trước/sau; postinstall/audit xác minh từng installed copy. Bốn regression theo entrypoint kiểm tra sync/async và không duyệt phần tử vượt giới hạn; tổng dependency regression 19/19 đạt. Không thêm ngoại lệ bỏ qua advisory theo tên/version.

## Verification trong vòng này

| Bước | Kết quả thực tế |
| --- | --- |
| Build | Solution Release đạt, 0 warning/error; hai portal production build đạt; APK debug x86_64 build và cài đạt. Native dependency còn warning deprecation/Gradle, chưa coi là sạch warning. |
| Type check | Mobile strict TypeScript và các workspace web đạt. |
| Lint/format | Mobile/web ESLint đạt; whitespace format hai tệp C# sửa đạt. |
| Tests | .NET: 383 đạt, 2 PostgreSQL skip / 385; Agent: 94/94 ngoài sandbox; mobile: 225/225; web: 275/275; browser E2E: 13/13. Mobile/web chạy lại sau backport Zod; E2E chạy sau đồng bộ lockfile native trước backport này. |
| Coverage | .NET 6755/8307 dòng = 81,32%; mobile lines 94,27%, statements 91,90%, functions 86,36%, branches 83,68%; web lines 95,48%, statements 92,71%, functions 88,91%, branches 87,46%. |
| Security/diff | Dependency gate gồm dev đạt, 19 regression đạt; native SDK check online đạt; diff whitespace đạt cho các tệp vòng này. Giữ tenant/user/key khi dedup, không đổi guard thao tác Agent. Raw npm audit vẫn có advisory cho source backport đã kiểm chứng. |

Lượt .NET đầu trong sandbox có 25 test Agent bị Windows từ chối truy cập DPAPI, thư mục tạm hoặc trạng thái phiên. Chạy lại riêng project Agent ngoài sandbox: 94/94 đạt. Báo cáo coverage bỏ kết quả Agent từ lượt sandbox lỗi và dùng kết quả chạy đạt; đường dẫn file được chuẩn hóa từ `sources` trước khi hợp nhất, không cộng lặp một tệp giữa các project.

Kết quả lịch sử của vòng review trước nằm trong `remediation-verification-2026-10-24.md`. Hai test PostgreSQL vẫn cần database thật.

## Bằng chứng và giới hạn

- Android thực sự gọi `/health/ready` qua alias emulator, nhận HTTP 200 và `status=ready`.
- APK riêng đã đăng nhập, tải thông tin máy VM và khôi phục phiên sau cold start; log, UI XML và ảnh emulator nằm trong scratch. APK debug x86_64 dùng Metro, chưa phải bản release cho điện thoại thật.
- Windows ban đầu lỗi BCD; đã đối chiếu màn hình và Panther logs. Tạo lại cả phân vùng Windows và EFI chỉ trên đĩa VM lab đã xác minh, áp ảnh Microsoft và chạy BCDBoot trong WinPE chính thức đã giải quyết lỗi specialize. Không sửa boot-store hoặc phân vùng của máy thật. Guest đã hoàn tất cài đặt và xác minh HTTPS; `windows-guest.json`, `guest-api-verification.json` nằm ngoài repository trong thư mục lab.
- Guest từng tự tắt do Evaluation chưa kích hoạt; event 1074 từ `wlms.exe` xác nhận nguyên nhân. Kích hoạt chính thức thành công: `LicenseStatus=1`, thời hạn ban đầu 129600 phút = 90 ngày; bằng chứng tại `windows-evaluation-activation.json` ngoài repo.
- Agent win-x64 self-contained chạy thành Windows Service `SentinelLANAgent`, Automatic, tài khoản `NT AUTHORITY\LocalService`. Binary cho service đọc/thực thi; dữ liệu chỉ LocalService, SYSTEM và Administrators. Task thử nghiệm trước đó đã gỡ để tránh hai instance. Đã restart VM bằng Windows: service tự chạy khi chưa đăng nhập desktop, HTTPS đạt và API ghi nhận heartbeat Online. Bằng chứng: `agent-service-verification.json`, `agent-service-reboot-verification.json` và `agent-server-verification.json`. Mode vẫn Development; command polling tắt, mọi cờ tác động OS giữ false. Chưa nghiệm thu identity/DPAPI Production, MSI/uninstall hoặc khóa/cách ly thật.
- Demo employee được gán đúng `SLAN-LAB-01` qua API có xác nhận/lý do, thay máy seed chỉ trong API lab.
- File trạng thái, log và ảnh màn hình riêng của emulator/VM nằm trong `scratch/lab-setup-2026-10-24/` bị Git ignore; native build nằm trong thư mục lab ngoài repository.
- Ba checkpoint đã xác nhận trong `windows-checkpoints.json`: Windows sạch, lab trước enrollment và `Lab ready - Agent service verified` sau kiểm chứng service/reboot.

API InMemory mất dữ liệu/phiên khi khởi động lại; Agent cần đăng ký lại nếu API mất enrollment. Default Switch có thể đổi IP, phải cập nhật mapping/firewall đúng guest. Chứng chỉ lab và Windows Evaluation có thời hạn. Chưa kiểm thử APK ARM/điện thoại thật, iOS hoặc FCM/EAS push; không dùng các kết quả lab này để tuyên bố toàn bộ hệ thống đã nghiệm thu production.
