# Vận hành VPS từ Super Admin

Mở portal chủ hệ thống, chọn tab Hạ tầng rồi **Kết nối VPS**. Nhập tên, host, cổng SSH, tài khoản SSH, private key và fingerprint xác minh qua console VPS tin cậy:

```sh
ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256
```

Private key được mã hóa trong vault backend; không lưu localStorage hoặc đưa vào tài liệu/repository. Kết nối dùng fingerprint đã pin. VPS cần Linux/systemd và coreutils; tài khoản SSH cần quyền đọc Docker daemon để lấy số liệu container. Việc có quyền Docker là quyền quản trị mạnh, phải giới hạn tài khoản và nguồn SSH ở hạ tầng.

## Kiểm tra

Chọn chu kỳ 1–5 phút, mặc định 3 phút. Kiểm tra khi mở tab; **Kiểm tra lại ngay** kiểm tra tất cả VPS, hoặc **Kiểm tra VPS này** để kiểm tra một máy. Vòng tiếp theo bắt đầu sau khi vòng trước kết thúc. Tab ẩn/rời tab dừng lịch; trở lại sau khi quá chu kỳ sẽ kiểm tra lại. Các nút cấu hình bị khóa khi đang lấy số liệu/thực thi để tránh chồng yêu cầu trong cùng trang.

Bảng hiển thị trạng thái SSH, thời gian kiểm tra, CPU thực, RAM host, ổ đĩa `/`, uptime, systemd, trạng thái/tự chạy của dịch vụ, danh sách container gồm cả đã dừng. Health của container phụ thuộc healthcheck đã cấu hình. RAM tiến trình API ở phần Backend không đại diện RAM toàn VPS.

Dung lượng Docker lấy từ `docker ps --size`: lớp ghi và image “virtual” có thể dùng chung, chưa bao gồm volume và log. Block I/O là dữ liệu đọc/ghi, không phải dung lượng đang chiếm. Docker không truy cập được được báo là lỗi/chưa xác định, không coi là không có container.

## Lệnh chuẩn

| Thao tác UI | Lệnh trên VPS | Xác minh |
|---|---|---|
| Kiểm tra hoạt động | `systemctl is-system-running`, `systemctl is-active SERVICE`, `systemctl is-enabled SERVICE` | Trạng thái thực tại lần kiểm tra |
| Docker | `docker ps -a --no-trunc --size --format '{{json .}}'`, `docker stats --no-stream --no-trunc --format '{{json .}}'`, inspect chọn lọc | ID, health, restart policy và số liệu container |
| Khởi động lại dịch vụ | `systemctl restart SERVICE` | `systemctl is-active --quiet SERVICE` |
| Bật tự chạy | `systemctl enable SERVICE` | `systemctl is-enabled --quiet SERVICE` |
| Tắt tự chạy | `systemctl disable SERVICE` | Trạng thái `disabled` |
| Cấu hình container | `docker update --restart=POLICY FULL_CONTAINER_ID` | Inspect lại `HostConfig.RestartPolicy.Name` |
| Reboot VPS | `shutdown -r +1` | Lệnh được chấp nhận; lần kiểm tra tiếp theo xác minh máy chủ trở lại |

`SERVICE` chỉ là `nginx`, `docker`, `sentinellan-agent`, `cron`, `systemd-resolved`; đây là danh sách cho phép, không phải danh sách bắt buộc cài. `FULL_CONTAINER_ID` là 64 ký tự hex lấy từ probe. `POLICY` chỉ là `no`, `always`, `unless-stopped`. Lệnh thay đổi thử quyền hiện tại rồi `sudo -n` nếu cần, không tương tác nhập mật khẩu. Cấp quyền qua chính sách vận hành đã duyệt; không cấp `NOPASSWD: ALL` để làm UI hoạt động.

`enable/disable` thay đổi tự chạy khi boot, không start/stop dịch vụ ngay. Với container, bật tự chạy dịch vụ Docker trước. `always` tự chạy khi Docker trở lại; `unless-stopped` giữ trạng thái dừng thủ công. Cấu hình policy không start container đã dừng ngay. Lưu cùng `restart:` vào Compose của dịch vụ để giữ cấu hình khi tạo lại container. Xem [Docker restart policy](https://docs.docker.com/engine/containers/start-containers-automatically/).

Reboot cần lý do và xác nhận rõ máy đích, sẽ tạm gián đoạn dịch vụ. Nếu API/DB đang trên VPS này, UI cũng mất kết nối trong lúc reboot; thử kiểm tra lại sau khi máy chủ trở lại. Trạng thái “đã nhận lịch” không đảm bảo hệ điều hành đã reboot. Trước reboot nên xác minh Docker enabled và policy từng container cần hoạt động sau boot.

## Contract API

Các endpoint chỉ cho `PlatformOwner`, dùng cookie riêng và CSRF header `X-SentinelLAN-CSRF: 1`:

- `GET /api/v1/platform/vps-nodes`: cấu hình và số liệu tổng lưu gần nhất, `runtime` chưa lấy trong request này.
- `POST /api/v1/platform/vps-nodes/{id}/refresh-metrics`: lấy mẫu SSH mới, trả `VpsNodeDto` kèm `runtime { systemState, dockerAvailable, dockerError, services, containers }`. Thiếu node trả 404; SSH/probe thất bại trả node có trạng thái Error. Mỗi container có `id`, `name`, `image`, `state`, `status`, `health`, `restartPolicy`, `cpuPercent`, `memoryUsage`, `memoryPercent`, `storageUsage`, `networkIo`, `blockIo`.
- `POST /api/v1/platform/vps-nodes/{id}/operations`: `{ action, target, value, reason, confirmed, nonce, expiresAt }`. Action `Reboot` có target/value null; startup action có target service/value null; `SetContainerRestartPolicy` có target ID/value policy.
- `POST /api/v1/platform/vps-nodes/{id}/restart-service`: contract hiện hữu gồm serviceName, reason, confirmed, nonce, expiresAt.

Thao tác thay đổi trả `{ success, message, output }`; 200 khi lệnh được chấp nhận/xác minh, 400 khi không hợp lệ, hết hạn, nonce đã dùng hoặc thực thi thất bại. Nonce cần UUID mới, thời hạn UTC trong tối đa 5 phút và lý do 3–1000 ký tự. Nonce đã giữ sẽ không được dùng lại dù lệnh thất bại. Audit ý định và kết quả là append-only. OpenAPI Development `/openapi/v1.json` phát sinh từ route/DTO, có schema snapshot và operation.

Render/lọc/tính thời gian chạy trong trình duyệt admin; SSH và bảo vệ khóa vẫn ở backend. Không có giám sát nền khi không mở màn hình. Nhiều admin có lịch độc lập; với nhiều VPS cần điều chỉnh chu kỳ theo rate limit hiện hữu (30 yêu cầu nhạy cảm/phút/nguồn).
