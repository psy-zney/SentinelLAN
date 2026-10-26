# Đồng bộ release và tương thích Agent/backend

Không yêu cầu Agent và backend có cùng số phiên bản. Backend tiếp nhận heartbeat hợp lệ từ phiên bản Agent khác, giữ đầy đủ xác thực, nonce và chống replay. `/api/v1/agent/compatibility` công khai commit/build và giao thức (không trả `.env`, credential hay khóa). Endpoint không có ở backend cũ không tự chứng minh máy chủ mất kết nối.

Agent giữ heartbeat khi backend không có API tùy chọn command/policy (404/405). WebSocket wake không kết nối được vẫn dùng polling. Lỗi 401/403, lỗi máy chủ hoặc heartbeat thất bại vẫn là lỗi, không bị biến thành thành công. Máy chủ chưa có bootstrap request-signing trả trạng thái `ServerUpgradeRequired`; không tự gửi credential qua cơ chế yếu hơn để né xác thực.

Agent đời rất cũ chưa biết ký request cần nâng cấp một lần. Backend có cơ chế chuyển tiếp đã có sẵn: `SENTINELLAN_AGENT_LEGACY_AUTH_UNTIL` là thời hạn UTC tối đa bảy ngày, chỉ IT bật khi quản lý được rủi ro. Credential đã chuyển sang ký request không được quay lại legacy. Không dùng chế độ này như cấu hình thường trực.

## Cấu hình

- Windows: cấu hình mặc định của bộ cài ở `%ProgramData%\SentinelLAN\Agent\agent-settings.json`; biến môi trường và CLI được chỉ định rõ có ưu tiên cao hơn. Company build vẫn từ chối URL khác công ty dù có override.
- Linux: systemd đọc file EnvironmentFile do trình cài tạo. Không tự đọc `.env` bất kỳ trong working directory.
- VPS: `compose-secure.sh` đọc đầy đủ file Compose environment ở `/opt/sentinellan/shared/.env`; có thể chọn đường dẫn bằng `SENTINELLAN_ENV_FILE`. Không `source` file này hoặc in giá trị bí mật. Compose chỉ đưa các biến thuộc service tương ứng vào container. File env thiếu/không đọc được phải báo lỗi trước khi chạy.
- `SENTINELLAN_AGENT_REQUIRE_SIGNED_REQUESTS` là tên chính xác, đồng nhất giữa Compose và API; tên cũ `SENTINELLAN_REQUIRE_SIGNED_AGENT_REQUESTS` vẫn được đọc. Production luôn bắt buộc ký trừ cửa sổ migration có thời hạn. Cấu hình push Expo cũng được chuyển vào API.

## Đưa cùng release lên VPS

Release chỉ chứa `SentinelLAN.Setup.exe`; checksum và source commit nằm trong mô tả GitHub Release. API/company/employee images có OCI revision label; API gắn commit vào assembly khi build. Mỗi bản release phải build từ commit đã push và tag chính xác, không dùng lại asset cũ cùng tên.

Trên VPS có checkout sạch của tag được chọn, GitHub CLI đã đăng nhập, TLS hiện có và file môi trường được bảo vệ:

```bash
python3 deploy/vps/deploy-github-release.py --tag v1.0.2 --check-only
python3 deploy/vps/deploy-github-release.py --tag v1.0.2
```

Script từ chối checkout bẩn/tag sai, release draft/prerelease, source commit không khớp hoặc bộ asset khác một Setup EXE. Kiểm tra env bằng Compose không in giá trị. Download installer qua GitHub CLI, xác minh SHA-256 trước khi build/up. Chỉ thay bộ cài được tải trên VPS sau khi endpoint API xác nhận đúng commit và fingerprint public key ký lệnh khớp Setup. Giữ installer trước ở `SentinelLAN.Setup.previous.exe`; file `agent-release.json` ghi version/commit/hash công khai. Không tự đổi signing key, token, database volume hoặc chứng chỉ.

Trước triển khai cần giữ bản sao lưu theo quy trình vận hành hiện có; thay schema database cần kế hoạch rollback riêng. Script không đảm bảo rollback database và không thay thế nghiệm thu enrollment/heartbeat từ máy Windows thật.

## Giới hạn kiểm chứng hiện tại

Đã kiểm tra kết nối GitHub và chạy các regression tương thích trong cloud. SSH từ phiên cloud tới `149.118.50.176:22` bị từ chối, nên chưa đọc được commit đang chạy trên VPS, chưa chạy deploy script trên VPS và chưa xác nhận heartbeat của máy người dùng. Không coi commit đã push hoặc bộ test tự động là bằng chứng đã đồng bộ production.
