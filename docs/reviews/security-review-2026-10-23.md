# Security review SentinelLAN — 23/10/2026

> **Đã sửa SR-01.** Kết quả và bằng chứng mới nhất tại [verification sau sửa lỗi, 24/10/2026](remediation-verification-2026-10-24.md). Nội dung dưới đây giữ nguyên kết quả tại thời điểm review.

**Kết quả:** xác nhận một lỗi mới P2 trong luồng xác nhận thu hồi, xuất hiện ở hai endpoint. Đã tái hiện bằng fixture HTTP cô lập, kiểm tra trạng thái dữ liệu sau thao tác và chạy lại 51 test bảo mật trọng điểm. Chưa có bằng chứng mới về vượt quyền tenant/RBAC, thực thi lệnh tùy ý, SQL injection hay XSS trong phần đã duyệt.

Áp dụng skill `security-review`, hướng dẫn `sentinellan-dev` và ba agent chuyên môn; parent đối chiếu và kiểm chứng kết luận. Review trên working tree sau [báo cáo verification](graduation-verification-2026-10-23.md), HEAD `0c309a4`; không sửa source sản phẩm, dependency hay triển khai. Đây là review có mục tiêu theo checklist, không phải pentest hay chứng nhận toàn bộ hệ thống an toàn.

## Phát hiện xác nhận

### SR-01 — P2: DELETE không có body được tự xác nhận

**Vị trí:**

- [UserEndpoints.cs:79](../../apps/backend/src/SentinelLAN.Api/Endpoints/UserEndpoints.cs#L79): `req ?? new RevokeActivationTokenRequest("Revoked by administrator", true)`.
- [QrEndpoints.cs:33](../../apps/backend/src/SentinelLAN.Api/Endpoints/QrEndpoints.cs#L33): `req ?? new RevokeQrLabelRequest("Revoked by technician", true)`.

**Điều kiện:** phiên Admin hợp lệ trong đúng tổ chức, có header CSRF hợp lệ khi dùng cookie, và có activation invitation hoặc QR label đang hoạt động. Quyền Admin, tenant scope và CSRF vẫn được áp dụng.

**Hành vi:** hai handler nhận body nullable. Khi không có body, tầng API tự tạo `Confirmed=true` cùng lý do mặc định rồi truyền xuống Application. Vì vậy các validation yêu cầu lý do và xác nhận không thể phân biệt việc người gọi thực sự cung cấp các giá trị này hay bị máy chủ tự điền. Điều này vi phạm yêu cầu của AGENTS rằng thao tác nhạy cảm phải có lý do và xác nhận; audit lưu lý do do máy chủ tạo thay cho lý do của người thực hiện.

**Bằng chứng thực thi:** probe `MissingDeleteBodyCurrentlyRevokesWhileFalseConfirmationIsRejected` dùng `SentinelApiFactory` với EF InMemory mới, chặn chạy nếu có PostgreSQL connection string. Không gửi HTTP ra bên ngoài và không sử dụng dữ liệu thật.

| Tình huống | HTTP quan sát | Trạng thái dữ liệu |
|---|---:|---|
| Admin DELETE invitation, body có `confirmed=false` | 400 | Thao tác bị từ chối |
| Anonymous DELETE invitation | 401 | Thao tác bị từ chối |
| Admin DELETE invitation, không body | 204 | `AccountActivationToken.RevokedAt` có giá trị |
| Admin DELETE QR, body có `confirmed=false` | 400 | Thao tác bị từ chối |
| Admin DELETE QR, không body | 204 | QR label vừa tạo có `RevokedAt` |

Probe cuối chạy đạt 1/1 vì mục đích của nó là **xác nhận hành vi lỗi hiện tại**, không phải quality gate cho rằng hành vi này an toàn. Bản probe đầu truy vấn tất cả label của thiết bị bằng `SingleAsync`, gặp cả label seed và label vừa tạo; đã sửa probe để truy vấn đúng ID label mới, rồi chạy lại thành công. Không tính lỗi truy vấn của probe là lỗi sản phẩm.

**Mức độ:** P2, độ chắc chắn cao. Tác động giới hạn ở việc bỏ qua bước xác nhận/lý do cho thao tác mà Admin vốn có quyền thực hiện và độ chính xác lý do audit; không có bằng chứng cấp thêm quyền hay tác động sang tenant khác.

**Sửa nhỏ nhất:** yêu cầu body khác null; trả 400 khi body thiếu hoặc JSON null. Giữ `Reason`/`Confirmed` do client cung cấp và để Application tiếp tục kiểm tra. Bổ sung regression test cho cả hai endpoint: thiếu body, JSON null, `confirmed=false`, lý do rỗng phải bị từ chối và dữ liệu không thay đổi; body hợp lệ phải thu hồi và ghi đúng lý do. Đây là áp dụng yêu cầu dự án về xác nhận, phù hợp nguyên tắc kiểm soát bước nghiệp vụ ở server trong [OWASP Transaction Authorization](https://cheatsheetseries.owasp.org/cheatsheets/Transaction_Authorization_Cheat_Sheet.html#21-authorization-should-be-performed-and-enforced-server-side).

## Đối chiếu checklist

| Nhóm | Bằng chứng từ nguồn / kết quả | Giới hạn |
|---|---|---|
| Bí mật | Không thấy tệp `.env`, private key/certificate thuộc mẫu đã chọn đang được track. Quét regex lịch sử các đường dẫn source/deploy/scripts trên lịch sử có 82 commit chỉ khớp placeholder i18n ở `d4bdd50`, đã triage. | Không đọc credential thật; không có gitleaks/trufflehog sẵn. Regex/path scan không bao phủ mọi dạng secret, toàn bộ Git object hay cấu hình hosting. |
| Input và upload | Self-service kiểm tra loại yêu cầu, độ dài, idempotency; ảnh chỉ PNG/JPEG, magic bytes, tối đa 2 MiB, cấm control/path trong tên; tối đa 10 ảnh/request và quota theo user/ngày/tenant. | Chưa fuzz parser, kiểm thử image decoding hoặc kiểm chứng tải/DB production. |
| SQL | Truy vấn EF có organization/request/user scope; hai raw statement đang dùng `ExecuteSqlInterpolatedAsync` với tham số cho advisory lock. | Không khẳng định đã đọc mọi truy vấn/module. |
| Auth/session | Kiểm tra expiry, trạng thái user, organization, role/security stamp; refresh hash, rotation và thu hồi family khi reuse; activation một lần và hạn dùng. | Race nhiều replica và PostgreSQL thật chưa kiểm chứng trong lần này. Access token của logout một phiên còn có thể sống đến expiry theo thiết kế hiện tại. |
| Authorization | Endpoint policy + Application role/tenant/ownership; Employee self-service cần đúng user và thiết bị đang được gán; DTO QR riêng tư chỉ trả sau kiểm tra scope. | SR-01 còn bỏ qua yêu cầu xác nhận/lý do ở hai thao tác thu hồi. |
| CSRF/CORS | Web cookie HttpOnly, Secure ngoài Development, SameSite Strict; thao tác ghi dùng custom header; CORS dùng allowlist origin HTTPS cụ thể. | Phụ thuộc giữ đúng allowlist và proxy/config khi triển khai. |
| XSS/CSP | React render text; chưa thấy raw HTML/eval sink trong frontend được duyệt, không lưu auth token vào localStorage. | CSP ở một số nginx host template thiếu `script-src/default-src`; Next config chưa CSP. Đây là điểm tăng cường, chưa phải XSS đã tái hiện. |
| Rate limit | Có ingress/IP và sensitive policy; login web/mobile dùng chung account budget lưu DB. | Lỗi tốc độ token bucket đã ghi ở báo cáo trước; chưa có load test hay đánh giá nhiều replica. |
| Dữ liệu, lỗi và log | API nghiệp vụ trả lỗi có kiểm soát; ảnh download `private,no-store` + `nosniff`; ảnh object store có key theo tenant/request/attachment, AES-GCM và purpose binding theo IDs. | Không đọc log thực tế; chưa chứng minh cấu hình logging/ACL, rotation khóa, bucket policy hay backup khi triển khai. |
| Agent và OS | Verifier kiểm tra device/type/thời gian/chữ ký/nonce trước thực thi; OS action cần opt-in lab; lệnh và service trong allowlist; Linux dùng ArgumentList, PowerShell dùng script cố định và JSON env. | Chưa nghiệm thu quyền native OS, ACL, firewall recovery hay deadline sau khi OS action đã bắt đầu. |
| Cài phần mềm | Local opt-in/elevation, HTTPS exact trusted host, cấm redirect, size cap, SHA-256, MSI header, Authenticode/publisher và kiểm tra lại approval trước cài. | Chưa chạy Authenticode/msiexec thật trong lần này. Host nội bộ được IT tin cậy là use case LAN, chưa có đường Employee tự chọn đích. |
| Dependency | Giữ kết quả cùng working tree ở báo cáo verification: gate có xác minh backport đạt, còn Moderate và cảnh báo dev tooling; NuGet không liệt kê advisory khi quét. | Không chạy lại npm/NuGet audit trong lượt này; đây không phải bảo đảm không có lỗ hổng chưa công bố. |
| Supabase / blockchain | Không áp dụng cho kiến trúc này. | Không coi thiếu Supabase RLS hay wallet check là lỗi của PostgreSQL/EF. |

Custom header API không bắt buộc phải là một synchronizer token ngẫu nhiên khi kết hợp đúng CORS và các điều kiện tương ứng; không đánh dấu CSRF lỗi chỉ vì header hiện là hằng số. [OWASP mô tả phương án custom request header cho AJAX/API](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html#employing-custom-request-headers-for-ajaxapi).

## Triage để tránh kết luận sai

- Không tính việc QR cập nhật `LastScannedAt`/`DeviceQrScanned` trước khi từ chối mở chi tiết là lỗ hổng đã xác nhận. `docs/database/data-dictionary.md:19` định nghĩa đây là lần quét đã xác thực; Employee trong luồng đã xác thực. Không có yêu cầu chỉ cập nhật khi người quét được mở thông tin thiết bị. Có thể làm rõ outcome scan và access denied trong vận hành nếu cần.
- CSP chưa đủ chặt không tự chứng minh XSS; DNS của package host có thể trả IP nội bộ không tự chứng minh SSRF trong mô hình IT allowlist có chủ đích.
- Không tìm thấy tích hợp SSH/Vault runtime hiện hành trong Application/Infrastructure được duyệt; các dấu vết migration không được xem là endpoint đang chạy.

## Kiểm chứng đã chạy

| Lệnh / phạm vi | Kết quả |
|---|---|
| Integration: `SecurityFlowTests`, `SelfServiceIntegrationTests`, `OperationalSecurityRegressionTests` | 16/16 đạt, 0 skip, exit 0 |
| Agent: `CommandVerifierTests`, `SelfServiceCommandTests`, `AgentSignatureTests` | 35/35 đạt, 0 skip, exit 0 |
| Probe SR-01 | 1/1 tái hiện hành vi hiện tại, exit 0 |
| `git diff --check` trên source/config/docs | Đạt |
| Quét lịch sử mẫu secret | Exit 0 với `--no-ext-diff --no-textconv`; hai marker private key là placeholder i18n |

Đặt TEMP/TMP của process kiểm chứng vào `scratch/security-review-2026-10-23/temp`. Probe dùng package đã cache, restore từ thư mục nguồn rỗng, không tải/cài mới dependency; bước restore song song đầu không hoàn tất, bước tuần tự đạt. Không chạy lại toàn bộ build/tests, browser/native smoke hoặc audit dependency vì source sản phẩm không thay đổi và đã có verification trước.

Lệnh test chính:

```powershell
dotnet test apps/backend/tests/SentinelLAN.IntegrationTests/SentinelLAN.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~SecurityFlowTests|FullyQualifiedName~SelfServiceIntegrationTests|FullyQualifiedName~OperationalSecurityRegressionTests'
dotnet test apps/agent/tests/SentinelLAN.Agent.Tests/SentinelLAN.Agent.Tests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~CommandVerifierTests|FullyQualifiedName~SelfServiceCommandTests|FullyQualifiedName~AgentSignatureTests'
dotnet test scratch/security-review-2026-10-23/Probe/SecurityReview.Probe.csproj --configuration Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false
```

Bằng chứng và probe được giữ ngoài Git ở `scratch/security-review-2026-10-23/`: `Probe/AutoConfirmationProbe.cs`, `results/auto-confirmation-confirmed.trx`, `results/focused-integration.trx`, `results/focused-agent.trx`.

## Việc còn lại

Ưu tiên sửa SR-01 và thêm regression test từ chối missing/null body; xử lý hai lỗi vận hành đã ghi ở verification trước (limiter và raw audit dư). Sau đó hoàn thiện CSP tương thích Next, phân tích advisory/tooling còn lại và nghiệm thu PostgreSQL, HTTPS/proxy/headers, log redaction, native OS recovery trên lab. Chỉ các bước có bằng chứng thực thi mới được đánh dấu hoàn tất; không suy ra mức an toàn môi trường thật từ các test InMemory.
