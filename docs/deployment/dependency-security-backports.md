# Bản sửa dependency chưa có release upstream

CI dùng `npm run audit:dependencies`, giữ tiêu chuẩn chặn advisory mức high/critical. Hai advisory đã được sửa trong source cài đặt: `GHSA-vfj7-8cjw-p6xm` (braces 3.0.3) và `GHSA-86w9-cpqp-85rv` (node-forge 1.4.0). Báo cáo npm theo version vẫn liệt kê chúng, nên kiểm tra CI phân biệt source có bản vá đã xác minh và source gốc còn lỗi.

`npm ci` chạy postinstall áp dụng manifest tại `scripts/dependency-security/patches.json`. Manifest ghi version chính xác, SHA-256 trước/sau và từng đoạn thay đổi. Source khác checksum hoặc version không được sửa tự động. Chạy lại trên source đã sửa không thay đổi file. Docker web copy scripts trước khi cài dependency; workspace chỉ cài web có thể không chứa công cụ mobile, nên installer bỏ qua module vắng mặt.

Braces giới hạn độ sâu stack khi parse và kiểm tra AST bằng vòng lặp trước các walker compile/expand/stringify, kể cả AST do caller truyền vào hoặc có cycle. Các pattern thông thường, range, escape và pattern nhiều lựa chọn vẫn có test. Node-forge kiểm tra số phần tử của DigestAlgorithm và yêu cầu NULL parameter rỗng khi xác minh chữ ký PKCS#1 v1.5. Bản sửa dựa trên [PR upstream 1152](https://github.com/digitalbazaar/forge/pull/1152), thêm kiểm tra NULL theo RFC. Các test dùng chữ ký từ Node crypto, DigestInfo có phần tử dư và NULL không hợp lệ; chỉ sinh khóa trong bộ nhớ của test.

Trước audit, CI chạy test khai thác trên module thực tế đã cài. Sau đó gọi `npm audit --omit=dev --json`, kiểm checksum của mọi vị trí module thuộc hai advisory cụ thể. Advisory mới trong cùng package, file thiếu/bị đổi, version khác và lỗi mạng/JSON đều làm kiểm tra thất bại. Các advisory chưa sửa vẫn hiển thị, và high/critical chưa sửa vẫn chặn CI; không miễn trừ toàn package hay bỏ qua dependency tools của mobile.

`npm audit --omit=dev --audit-level=high` chạy trực tiếp vẫn báo theo version npm gốc, không nhận biết source được backport. Dùng `npm run audit:dependencies` để xác minh source thực tế; log ghi rõ các backport và advisory còn lại. Những cảnh báo moderate của decode-uri-component, sprintf-js và uuid chưa nằm trong bản sửa này; tiêu chuẩn high/critical giữ nguyên.

Khi upstream phát hành bản vá, cập nhật dependency, gỡ pin và manifest/backport tương ứng sau khi các test khai thác đạt trên release mới. Không sửa hash để chấp nhận source mới nếu chưa review thay đổi. Tham khảo: [braces issue 70](https://github.com/micromatch/braces/issues/70), [node-forge advisory](https://github.com/advisories/GHSA-86w9-cpqp-85rv).

## Kết quả ngày 10/10/2026

Commit `3060d1ac462a87b346d289f9a19aa03af4afda59` đã lên main. [CI 37971342184](https://github.com/psy-zney/SentinelLAN/actions/runs/37971342184) hoàn tất thành công toàn bộ: 286 test .NET (121 integration với PostgreSQL), 14 collector test, 12 test backport, 36 web unit test, 62 mobile test và 8 browser E2E test. Cả audit, lint/typecheck web/mobile, build backend/web và kiểm tra Compose đều đạt. Bộ test backport trước khi sửa có 8 ca lỗi, sau khi sửa đạt 12/12. Lần cài sạch bằng `npm ci` trên Windows và CI Linux đều tự áp dụng 6 file thay đổi và xác minh checksum; workspace local sạch sau khi bỏ thay đổi file Next.js sinh ra từ build.

Các advisory moderate vẫn được liệt kê trong log. CI chưa miễn trừ chúng; tiêu chuẩn chặn high/critical không đổi. Không cập nhật container đang chạy trên VPS cho bản sửa công cụ npm này.
