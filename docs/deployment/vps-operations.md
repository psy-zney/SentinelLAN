# Giám sát host bằng JSON snapshot cục bộ

Cập nhật tháng 10/2026: Admin đơn vị vận hành xem `/api/v1/host/status`. API đọc snapshot cục bộ; SSH, portal PlatformOwner và restart/reboot VPS không còn được công bố.

## Cài collector tại host Linux

`deploy/vps/collect-host-status.py` chạy bằng systemd tại host. Sao chép `deploy/vps/host-monitor.conf.example` tới `/opt/sentinellan/shared/host-monitor.conf`, đặt root-owned mode 0600 và điền tên/địa chỉ host. Cài unit/timer tương ứng vào `/etc/systemd/system/`, chạy `sudo systemctl daemon-reload` và `sudo systemctl enable --now sentinellan-host-monitor.timer`.

Unit tham chiếu release `/opt/sentinellan/current/`. Timer chạy sau boot 30 giây, cập nhật mỗi 60 giây. Collector ghi nguyên tử `/opt/sentinellan/shared/host-monitor/status.json`; thư mục 0750 và file 0640, root:1654. API mount chỉ đọc, không có Docker socket hay SSH key.

| Biến cấu hình host | Ý nghĩa |
|---|---|
| `SENTINELLAN_HOST_NAME`, `SENTINELLAN_HOST_ADDRESS` | Tên hiển thị, địa chỉ kỹ thuật host |
| `SENTINELLAN_HOST_SERVICES` | Systemd unit phân cách dấu phẩy; mặc định nginx, docker, ssh, cron, systemd-resolved và timer collector |
| `SENTINELLAN_HOST_STORAGE_TARGETS` | JSON object: tên nhóm → paths (mảng đường dẫn), category (repo/service/system) |

Ví dụ JSON: `{"sentinellan-prod":{"paths":["/opt/sentinellan"],"category":"repo"}}`. Collector tự khám phá checkout qua Compose labels; tên nhóm lấy trực tiếp từ `com.docker.compose.project`, thiếu label vào nhóm `other`. Không suy đoán từ tên container. Thư mục bổ sung/cũ khai báo rõ; không có danh sách dự án cá nhân hardcode. Sau giải symlink, đường dẫn phải dưới `/opt`, `/srv`, `/var/www` hoặc `/home`; cha/con trong cùng nhóm không đếm hai lần.

Unit bật `ProtectHome=tmpfs`; muốn đo checkout dưới home cần drop-in `BindReadOnlyPaths` cho đúng thư mục được ủy quyền. Collector không lấy Docker environment, command hoặc log.

## Cấu hình API và quyền

- `SENTINELLAN_HOST_SNAPSHOT_PATH`: file trong container; Compose dùng `/run/sentinellan-host/status.json`.
- `SENTINELLAN_HOST_MONITOR_ORG_CODE`: mã đơn vị được xem; Compose dùng mã bootstrap.
- `HOST_MONITOR_DIRECTORY`: thư mục host mount chỉ đọc, mặc định `/opt/sentinellan/shared/host-monitor`.

Chỉ Admin có mã tổ chức khớp được đọc. API phân biệt chưa cấu hình với file chưa đọc được; file rỗng/JSON sai/trên 2 MiB không thành snapshot hợp lệ. Quá 3 phút hoặc tương lai hơn 1 phút được báo cũ; kiểm tra lại chỉ đọc snapshot, không chạy lệnh host.

## Số liệu và giới hạn

Snapshot gồm CPU, RAM, đĩa gốc, uptime, systemd/dịch vụ, cổng nghe, Docker container/health/restart policy và dung lượng. CPU lấy hai mẫu; RAM dùng MemAvailable. Đo dung lượng có timeout/ngân sách thời gian và cảnh báo khi chưa đo đủ.

Docker có lớp dùng chung, không cộng mọi số liệu thành dung lượng vật lý duy nhất. Snapshot không chứng minh host an toàn nếu root bị chiếm. Restart/reboot và thay đổi tự chạy do người vận hành thực hiện ngoài ứng dụng.
