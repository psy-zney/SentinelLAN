# Storage trong màn hình tình trạng VPS của Admin công ty

Collector `deploy/vps/collect-host-status.py` chạy trên host, ghi snapshot atomically. API chỉ đọc file và vẫn giới hạn cho Admin của đơn vị vận hành VPS. Không chuyển nhãn Docker, environment, lệnh hay log container vào snapshot.

Dung lượng Docker lấy từ `docker system df`. `kB/MB/GB/TB` dùng cơ số 1000; `KiB/MiB/GiB/TiB` dùng cơ số 1024. UI hiển thị GiB/MiB; ví dụ 17.98 GB là khoảng 16.75 GiB. Phép quy đổi dùng số thập phân để tránh sai số khi chuyển sang byte.

Danh sách thư mục gồm các vị trí vận hành hiện hữu và thư mục đang chạy Compose, phát hiện qua nhãn `com.docker.compose.project.working_dir`. Nếu Compose nằm trong thư mục con của Git checkout, collector đo từ Git root. Đường dẫn chỉ được phép dưới `/opt`, `/srv`, `/var/www`, `/home/ubuntu`; đường dẫn tương đối, symlink thoát khỏi phạm vi và đường dẫn không đọc được đều được bỏ qua kèm cảnh báo.

Đường dẫn alias và thư mục con nằm trong một thư mục đã đo của cùng dự án chỉ tính một lần. Bản checkout cũ và bản đang chạy ở hai vị trí riêng biệt đều chiếm disk và đều được tính. Docker image, volume và build cache là các mục toàn host; bảng thư mục không phân bổ các mục Docker dùng chung cho từng repo. Không cộng các hàng thành tổng disk đã dùng, vì có thể còn dữ liệu hệ thống và dữ liệu dùng chung.

Unit systemd dùng `ProtectSystem=strict`, `ProtectHome=tmpfs`, `NoNewPrivileges=true`. Chỉ bind read-only các thư mục dự án cần đọc dưới home: `exxplore-kittens`, `livekit`, `beatsync`, `go`. Các thư mục khác trong home vẫn bị ẩn. Với một dự án mới dưới home, quản trị viên cần thêm đường dẫn bind read-only cụ thể vào unit trước khi phép đo có thể đọc thư mục đó; không mở toàn bộ `/home`.

Docker storage có timeout 6 giây, mỗi phép đo thư mục tối đa 4 giây và toàn phần storage có ngân sách 20 giây. Một mục lỗi/timeout bị bỏ qua và thêm vào `warnings`; CPU, RAM, disk tổng, dịch vụ và container tiếp tục được cập nhật. Dữ liệu cũ của mục lỗi không được gán thời gian mới.

Kiểm tra collector:

```sh
python3 -B -m unittest discover -s deploy/vps/tests -v
systemd-analyze verify deploy/vps/sentinellan-host-monitor.service
```

CI chạy trên pull request, push lên `main` và yêu cầu thủ công; collector tests cũng nằm trong hai script `test-all`. API integration tests dùng PostgreSQL tạo schema riêng cho từng `SentinelApiFactory`, rồi dọn đúng schema sinh ngẫu nhiên khi factory được dispose. Chỉ đặt `ConnectionStrings__SentinelLAN` tới database kiểm thử biệt lập; tài khoản test cần quyền tạo và xóa schema trong database đó. SelfHost tests thay toàn bộ cấu hình provider cũ khi dùng InMemory để tránh đăng ký đồng thời PostgreSQL và InMemory.

## Kiểm chứng ngày 10/10/2026

Commit `e1fd17c98a3e151971a46554b20023dbbbfd0eed` đã lên `main`. Workflow CI còn bị tắt thủ công trên GitHub; đã bật lại và chạy xác minh commit này. [Run 37968157321](https://github.com/psy-zney/SentinelLAN/actions/runs/37968157321) đạt build, audit backend, 286 test .NET (bao gồm 121 integration test với PostgreSQL) và 14 test collector trên Linux. Các lỗi đăng ký hai provider EF Core và dữ liệu test dùng chung trước đây đã được giải quyết. Local lint, typecheck, 36 web unit test, test trình duyệt trang VPS và kiểm tra format các file C# đã đạt. Lượt test PostgreSQL qua SSH tại máy local được dừng sau khi bộ tương đương trên GitHub đã đạt đầy đủ; database và tunnel tạm đã được dọn.

CI còn dừng ở `npm audit --omit=dev --audit-level=high`: 60 mục cảnh báo (15 moderate, 45 high), bao gồm các chuỗi dependency của Expo/Metro. Một số package gốc vẫn chưa có bản vá phát hành: [braces](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm), [node-forge](https://github.com/advisories/GHSA-86w9-cpqp-85rv), [sprintf-js](https://github.com/advisories/GHSA-hp3w-g68c-fv3c). Các bước web phía sau audit bị skip trong CI; kết quả local được ghi riêng ở trên. Chưa thay dependency, hạ tiêu chuẩn audit hoặc miễn trừ các cảnh báo này.

VPS đang dùng release `20261010-vps-storage-798008cbad57c8a1`. Cập nhật chỉ collector và unit systemd; mọi ID container ứng dụng và checksum cấu hình production giữ nguyên. Kiểm chứng qua HTTPS: readiness 200; đọc host ẩn danh 401; Admin nhận snapshot mới, `no-store`, không có cảnh báo. Sau khi dọn container PostgreSQL kiểm thử riêng, còn đúng 13 container như trước. Đã đối chiếu byte của từng mục Docker với `docker system df`.

| Mục | Số byte đã xác minh | Đường dẫn |
| --- | ---: | --- |
| Mọt Mê Bánh | 14.594.048 | `/opt/apps/bep-du-banh/repo` |
| LiveKit | 12.288 | `/home/ubuntu/livekit` |
| Kitchen Explore | 59.772.928 | `/var/www/exxplore-kittens`, `/home/ubuntu/exxplore-kittens` |
| Docker Build Cache | 17.980.000.000 | khoảng 16,75 GiB trên UI |

Metadata triển khai và unit cũ được lưu trong `/opt/sentinellan/shared/deployments/20261010-vps-storage-798008cbad57c8a1`, quyền đọc giới hạn root. Hai lần thử trước đã dừng trước cutover hoặc tự rollback; ứng dụng không bị thay container. Cloudflare chặn User-Agent mặc định của Python (403/1010); công cụ xác minh khai báo User-Agent `SentinelLAN-Deployment-Verification/1.0` và đã xác minh thành công.

Sau đó, commit `3060d1a` sửa hai advisory high bằng backport source có checksum và test khai thác. [CI 37971342184](https://github.com/psy-zney/SentinelLAN/actions/runs/37971342184) đã đạt toàn bộ, gồm audit, build, web/mobile và 8 test trình duyệt. Chi tiết cơ chế và các advisory moderate còn lại nằm trong `docs/deployment/dependency-security-backports.md`.

## Mở chi tiết từng mục trên màn hình VPS

Commit f83c837 tách CPU, RAM, ổ đĩa và Docker thành các mục mở/thu gọn riêng; mặc định chỉ hiện tổng quan. Chọn mục khác đóng phần tài nguyên trước. CPU/RAM có số liệu riêng theo container; giao diện nêu rõ đây không phải danh sách mọi tiến trình trên host.

Mỗi dòng dung lượng mở ra đường dẫn được đo, số byte và phạm vi phép đo. Dịch vụ mở riêng và hiển thị các cổng của tiến trình tương ứng, kể cả bản ghi có nhiều tên tiến trình. Container có chi tiết image, trạng thái, CPU/RAM, cổng ánh xạ VPS -> container, cổng EXPOSE và socket đang nghe trong mạng container.

Bảng socket của host phân biệt loopback IPv4/IPv6, wildcard và địa chỉ cụ thể. Bấm từng cổng xem đầy đủ endpoint, tiến trình và ánh xạ Docker khớp địa chỉ/cổng/giao thức; tìm kiếm cũng nhận tên container và dự án. Nhãn phạm vi nghe không khẳng định truy cập được từ Internet vì còn firewall/reverse proxy. Không suy đoán tiến trình hay container khi không có dữ liệu.

Kiểm tra local đạt lint/typecheck cả web, 36 unit test, build production company/employee. Test Playwright trang VPS đã đạt trên desktop 1440px và mobile 390px, gồm bàn phím, mở/thu gọn, Nginx, EXPOSE khác publish, localhost IPv4/IPv6, lọc theo container, UDP thiếu process, dữ liệu cũ và xoá dữ liệu khi API trả 403. Lượt local dùng fixture auth biệt lập; CI vẫn đăng nhập backend thật và chỉ thay dữ liệu host bằng fixture.

Triển khai web ngày 10/10/2026: source release /opt/sentinellan/releases/20261010-vps-ui-f83c837, image sentinellan-prod-company:20261010-vps-ui-f83c837. Metadata và công cụ rollback nằm trong /opt/sentinellan/shared/deployments/20261010-vps-ui-f83c837. Chỉ thay container company và company-tls; reload cấu hình gateway Nginx trong container hiện hữu. 11 container còn lại giữ nguyên ID, image, trạng thái, thời điểm chạy và số restart; volume, secrets, cấu hình Compose và checksum collector không đổi. Source archive SHA256 32f5ac58ada6fcbd330540ce3be3edfc032c1388a3aad553f5c24820f2618b0f.

Kiểm chứng qua HTTPS tên miền production: readiness 200, /company/vps 200, API host ẩn danh 401 và bundle mới có điều khiển mở chi tiết cổng cùng nhãn localhost. Kiểm chứng TLS gateway nội bộ còn xác nhận Admin nhận snapshot mới và cache-control no-store. Bản image trước được giữ cho khôi phục tự động nếu kiểm tra cutover lỗi.

CI của commit f83c837 đã thành công toàn bộ: https://github.com/psy-zney/SentinelLAN/actions/runs/37980154109. Test E2E trên CI dùng backend thật cũng đạt sau thay đổi giao diện.

## Sơ đồ dịch vụ VPS

Commit 45fb264 thay bảng dịch vụ bằng sơ đồ tương tác: VPS -> dịch vụ hệ thống / dự án Docker -> container. Đường nhánh thể hiện thành phần thuộc VPS; không mô tả tuyến reverse proxy khi chưa có dữ liệu tuyến đó. Mỗi nút dịch vụ/container mở chi tiết riêng và thu gọn được; dự án mở nhánh container bằng summary. Khi chọn container từ một ánh xạ cổng trong chi tiết dịch vụ, nhánh dự án tương ứng tự mở. Bấm thu gọn chi tiết trả focus về nút đã chọn.

Chi tiết dịch vụ lấy socket theo tên tiến trình thực tế, có alias SSH/Docker/PostgreSQL và xử lý nhiều tên tiến trình. Liên kết sang container chỉ có khi địa chỉ, cổng và giao thức khớp binding Docker. Không dùng EXPOSE làm bằng chứng cổng đang nghe hoặc công bố. Các dịch vụ không có socket vẫn hiện trạng thái và ghi rõ chưa có cổng tương ứng.

Không đổi contract API, collector hay quyền Admin. Sơ đồ dùng snapshot hiện có và cập nhật theo lượt làm mới của màn hình VPS; khi API trả 403, cả sơ đồ và chi tiết bị gỡ.

Local: lint và typecheck cả web đạt, 39 unit test đạt (thêm 3 kiểm tra ý nghĩa dữ liệu nối sơ đồ), cả 2 build production đạt; E2E VPS đạt trên desktop và mobile. Đã xem ảnh chụp test ở hai kích thước, xác nhận nhánh/connector, nút đang chọn và bảng chi tiết hiển thị đúng. Ảnh test và logs được giữ ngoài Git.

Triển khai sơ đồ: release /opt/sentinellan/releases/20261010-vps-ui-45fb264; image sentinellan-prod-company:20261010-vps-ui-45fb264; archive SHA256 fa68a57aced7b22da3c5b2abb51bee22bba91fb40f4d75725fee876640ec754e. Metadata ở /opt/sentinellan/shared/deployments/20261010-vps-ui-45fb264. Kiểm chứng HTTPS public đạt readiness 200, trang company/vps 200, host API ẩn danh 401 và bundle có vps-service-diagram. Admin nhận snapshot mới, no-store. 11 container ngoài company/company-tls, mọi volume, cấu hình/secret và collector giữ nguyên; rollback giữ image f83c837.

CI 37982225030 của commit 45fb264 đã thành công toàn bộ, gồm test trình duyệt trên backend thật: https://github.com/psy-zney/SentinelLAN/actions/runs/37982225030.
