# Tài liệu nội bộ SentinelLAN

Đối chiếu mã nguồn ngày 01/10/2026. Thư mục này bị Git và Docker build context bỏ qua. Không lưu secret thật trong tài liệu.

- [Quyền, luồng và nhiều VPS](permissions-and-flows.md).
- [Xử lý mất kết nối/sự cố](outage-recovery.md).
- [Vận hành VPS](../deployment/vps-operations.md), [triển khai](../deployment/saas-vps-guide.md).
- [Phân phối lệnh Agent](../architecture/command-delivery.md), [bản đồ hệ thống](../system-map.html), [threat model](../security/threat-model.md).
- `archive/README-before-public-docs.md` giữ hướng dẫn phát triển/triển khai trước khi thu gọn README công khai.
- `archive/site-before-public-docs.html.txt` giữ nội dung trang thiết kế cũ trước khi thay trang giới thiệu.

Tài liệu cũ giữ làm tham chiếu. Khi khác biệt về quyền VPS, dùng tài liệu quyền/luồng mới: VPS thuộc PlatformOwner, không thuộc Admin/Technician công ty.

## Chính sách công khai

Trong `docs/`, chỉ bốn tệp được Git theo dõi: `downloads.md`, `site/index.html`, `site/site.css`, `site/downloads.js`. README gốc chỉ giới thiệu và liên kết tải. CI kiểm tra danh sách docs và không cho portal riêng bị theo dõi; Pages cũng kiểm tra danh sách và đóng gói đúng ba tệp site, không đóng gói bản đồ thiết kế. `apps/platform/` vẫn riêng tư và không có trong Git.

Tài liệu thiết kế/ADR/vận hành được bỏ khỏi index nhưng giữ trên máy này. Clone mới không có chúng và không có giao diện nền tảng; cần phân phối nội bộ qua kho được bảo vệ và sao lưu riêng. Không dùng `git add -f` để đưa chúng lên repo công khai.

Markdown ngoài `docs/` cũng mặc định riêng tư, chỉ README gốc và hướng dẫn tải là công khai. `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, hướng dẫn Maestro `apps/mobile/e2e/README.md` và chứng chỉ `deploy/certificates/README.md` giữ cục bộ. Các liên kết nội bộ của chúng chỉ có nghĩa ở bản checkout đầy đủ này; không còn dẫn tới tệp đã gỡ trên GitHub.

`.gitignore` không xóa nội dung khỏi lịch sử Git, clone, artifact Pages hoặc Release cũ. Không tự viết lại lịch sử/force push. Nếu cần gỡ bản từng công bố, phải kiểm kê và phối hợp quy trình riêng. Secret từng công bố phải thu hồi/xoay; ignore không làm chúng an toàn trở lại.
