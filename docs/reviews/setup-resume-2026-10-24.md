# Setup tiếp tục trên máy đã đăng ký — 24/10/2026

## Thay đổi trong 0.2.26

- Máy chưa có identity giữ bước nhập mã. Máy đã có identity tự ẩn ô mã và chạy bước kiểm tra/khôi phục kết nối khi mở cửa sổ.
- Tiếp tục bằng URL hiện có, kiểm tra khớp máy chủ của bộ cài công ty. Không thay public key hoặc yêu cầu token mới cho nhánh tiếp tục.
- Script `ExistingIdentityOnly` từ chối khi identity đã mất; không tự chuyển sang đọc token. Setup đọc lại tình trạng identity khi thử tiếp: lỗi sau khi đã lưu identity chuyển sang tiếp tục; nếu identity mất thì trở lại nhập mã.
- Kết nối chỉ thành công khi dịch vụ khởi động lại và ghi xác nhận Connected mới. Thành công có **Hoàn tất** để đóng; lỗi có nút thử lại. Nút **Đóng** chờ thao tác cài/cấu hình hiện hành kết thúc trước khi đóng để cleanup an toàn.

## Kiểm chứng

| Phạm vi | Bằng chứng |
| --- | --- |
| 14 ca điều hướng/cấu hình | Đạt trên Linux SDK: nhánh đã đăng ký không gọi logic nhập mã; lỗi sau lưu identity không dùng lại mã; retry và mất identity; URL hợp lệ/sai công ty; lỗi JSON/URL không lộ nội dung cấu hình. [TRX](../../dist/windows-x64-0.2.26/setup-flow.trx). |
| 7 ca script Windows | Đạt ngoài sandbox: bốn trạng thái enrollment cũ, tiếp tục thành công/offline, mất identity. Service/ACL được mock; kiểm tra recovery trước Start-Service, token không được ghi ở nhánh tiếp tục, identity và cấu hình cũ được giữ. [TRX](../../dist/windows-x64-0.2.26/test-results/windows-setup-script-unrestricted.trx). |
| Build | MSI/EXE company-pinned 0.2.26 đã tạo; SHA256 của MSI nhúng trùng MSI phát hành. [Cấu hình](../../dist/windows-x64-0.2.26/configuration-check.json), [checksum](../../dist/windows-x64-0.2.26/SHA256SUMS.txt). |
| Format/diff | Folder-mode whitespace và diff whitespace đạt cho source sửa trong lượt này. |

Lượt test Windows có DLL Enrollment bị Application Control chặn: 11 đạt, 10 lỗi môi trường; nhóm 14 ca thuần logic được chạy lại trên Linux như trên. Lượt 7 ca script trong sandbox bị AuthorizationManager chặn; chạy lại ngoài sandbox đạt, không thay ExecutionPolicy hoặc chính sách Windows. Không coi các lượt lỗi là đạt.

Chưa nghiệm thu GUI 0.2.26 hoặc chạy script với service/ACL thật trong lượt này. Các test trên không thay thế nghiệm thu Windows MSI upgrade, reboot, service hỏng hoặc mất mạng thật. Bộ cài mới chưa thay thế download trên VPS hoặc bộ cài đã cài trên máy người dùng.
