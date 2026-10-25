# Kiểm chứng các luồng SentinelLAN — 25/10/2026

## Phạm vi và cách đọc

Kiểm tra working tree hiện có trên Windows 11, Node.js 24.18.0, npm 11.16.0 và .NET SDK 10.0.401. Không triển khai lên VPS, không dùng database nghiệp vụ, không cài/gỡ Agent thật và không bật hành động khóa/cách ly thiết bị.

“Đạt tự động” chỉ áp dụng cho ca và môi trường được ghi. Kiểm thử API dùng WebApplicationFactory; E2E dùng Chromium với hai portal thật và API Development/InMemory cục bộ. Một số ca giao diện chủ động chặn response để thử lỗi/phân quyền. Kiểm thử mobile dùng React Native Testing Library/Jest, kèm kiểm tra metadata SDK và export Android bằng Metro; chưa phải nghiệm thu APK trên điện thoại thật.

## Kết quả

| Cổng kiểm tra | Kết quả và phạm vi |
|---|---|
| Restore/build .NET Release | [Đạt](../../.tmp-verification/flow-audit-2026-10-25/dotnet-build.log), 0 warning/error với `TreatWarningsAsErrors=true`; build lại integration tests sau sửa fixture cũng đạt. |
| Build web production | [Đạt](../../.tmp-verification/flow-audit-2026-10-25/web-build.log) cả company và employee. |
| TypeScript/lint | Đạt các workspace web và mobile, ESLint không warning. |
| .NET baseline, có coverage | [465 Passed, 2 NotExecuted / 467](../../.tmp-verification/flow-audit-2026-10-25/dotnet-tests.log). Hai ca bỏ qua cần PostgreSQL. |
| Integration trên PostgreSQL 18.6 riêng | [192/192 Passed, 0 Failed, 0 NotExecuted](../../.tmp-verification/flow-audit-2026-10-25/postgres-verified/admin_ZNEY_2026-10-25_00_49_45_net10.0.trx). Hai ca PostgreSQL bị bỏ qua trong baseline đều đã chạy đạt ở đây. |
| Web UI | [292/292 ca](../../.tmp-verification/flow-audit-2026-10-25/web-tests.log), 20 tệp kiểm thử đạt; coverage gate 80% đạt. |
| Mobile | [298/298 ca](../../.tmp-verification/flow-audit-2026-10-25/mobile-tests.log), 30 suite đạt; coverage gate 80% đạt. |
| Browser E2E | [13/13 đạt](../../.tmp-verification/flow-audit-2026-10-25/browser-tests.log), gồm luồng thật và ca phản hồi được chặn có chủ đích. |
| Expo SDK / Android export | [Metadata SDK đạt](../../.tmp-verification/flow-audit-2026-10-25/mobile-sdk.log); [bundle Hermes Android](../../.tmp-verification/flow-audit-2026-10-25/mobile-export.log) gồm 3.372 module được export thành công. Không suy ra native build hoặc push thật đã đạt. |
| Triển khai/host collector | [20/20 Python unittest đạt](../../.tmp-verification/flow-audit-2026-10-25/deployment-tests.log) ngoài sandbox; Compose development, LAN và VPS qua `config --quiet`. Không chạy container production. |
| Dependency | [Gate npm gồm dev đạt, 19/19 regression backport đạt](../../.tmp-verification/flow-audit-2026-10-25/dependency-audit.log); [NuGet kiểm tra 14 project](../../.tmp-verification/flow-audit-2026-10-25/dotnet-dependency-audit.json), không có advisory được trả về. Không đồng nghĩa kiểm toán bảo mật toàn diện. |
| Script kiểm tra tổng | [Bốn kịch bản PowerShell với công cụ giả đạt](../../.tmp-verification/flow-audit-2026-10-25/harness-checks.log): mặc định, API tùy chỉnh, bỏ qua browser, lỗi browser. Bash `-n` và PowerShell parser đạt. |

## Ma trận luồng và bằng chứng

Các lớp kiểm thử dưới đây ánh xạ tới kết quả TRX của lượt tương ứng; web/mobile ánh xạ tới log bộ kiểm thử. Không gán một test tích hợp là bằng chứng thực thi thao tác hệ điều hành thật.

| Luồng | Bằng chứng kiểm thử tiêu biểu | Mức kiểm chứng |
|---|---|---|
| Đăng nhập web, refresh, logout, tài khoản bị khóa/thu hồi phiên | `AuthenticationApiTests`, `UserStatusIntegrationTests`, `ExplicitRevocationTests`; `auth-lifecycle.e2e.ts` | API + browser thật |
| Mobile bootstrap, đăng nhập, refresh đồng thời, token phát lại, logout một/tất cả phiên | `MobileAuthIntegrationTests`; `auth-provider-lifecycle.test.tsx`, `session-failure-paths.test.ts`, `root-session-routing.test.tsx` | API + component |
| Tạo người dùng, phân quyền Admin/Employee, khác tổ chức | `DeviceAdministrationIntegrationTests`, `SecurityHardeningTests`, `TenantQueryFilterTests`; `admin-device-lifecycle.e2e.ts` | API + browser thật |
| Cấp mã Agent, đăng ký, khôi phục khi mất response, từ chối mã hết hạn/dùng lại | `EnrollmentRecoveryTests.LostResponseCanBeRecoveredOnlyWithOriginalDeviceProof`, `SameProofConcurrentRetriesCreateOneDeviceWhileDifferentProofIsRejected`; `EnrollmentRecoveryStoreTests` | API + Agent adapter |
| Gán/thu hồi máy, mỗi người dùng một máy hoạt động, đúng tenant | `DeviceAdministrationIntegrationTests`, `DeviceLifecycleIntegrationTests`, `DeviceConcurrencyTests`; `my-device-recovery.test.tsx` | API + component |
| QR công khai tối thiểu, QR xác thực, activation đúng vai trò | `ActivationAndQrIntegrationTests`, `EnrollmentConnectionCodeTests`; `qr-personal-device.e2e.ts`, `scanner-journeys.test.tsx` | API + browser/component; chưa camera thật |
| HMAC Agent: method/path/body/device, timestamp, nonce và chặn downgrade | `AgentRequestSigningTests.SignedRequestsInteroperateWithAgentAndReplayIsRejectedWithoutTransmittingTheDeviceSecret`, `ModifiedOrExpiredSignedRequestsFailBeforeAnEndpointExecutes` | API + mật mã thật |
| Telemetry/inventory/posture chỉ tới đúng người dùng, Unknown không tự giải quyết alert | `EndpointObservationIntegrationTests.AgentInventoryAndProtectionReachOnlyTheAuthorizedDeviceAndUnknownDoesNotRecoverAlerts`, `DeviceHealthMonitoringTests` | API; dữ liệu probe được điều khiển |
| Dashboard realtime, cập nhật chi tiết máy, heartbeat timeout, lỗi API không thành dữ liệu mẫu | Hai ca trong `device-telemetry-signalr.e2e.ts`; `realtime-lifecycle.test.tsx` | HTTP/SignalR + browser thật; ca lỗi dùng response chặn |
| Chính sách máy/phòng ban, refresh, bulk lệnh an toàn | `PolicyServiceTests`, `DepartmentAndEnrollmentTests`, `CommandLifecycleTests` | Unit/API; chưa áp mọi policy trên OS |
| Giao lệnh, lease/redelivery, hết hạn, biên lai idempotent và audit | `CommandLifecycleTests`, `RsaCommandSecurityTests`, `AgentCycleTests`; `SelfServiceCommandTests` | API + Agent Core; adapter OS có mô phỏng |
| Mất mạng, hàng đợi telemetry, biên lai sau restart và nonce bền vững | `AgentCycleTests.OfflineHeartbeatIsBufferedAndFlushedOnNextSuccessfulCycle`, `ProtectedPendingResultSurvivesRestartAndRetriesWithoutPollingOrReexecutingIt` | Lỗi transport được điều khiển; chưa mạng vật lý |
| Nhân viên xem đúng máy, báo sự cố/yêu cầu, gửi lại không tạo trùng | `SelfServiceIntegrationTests.EmployeeRequestIsAssignedScopedAndIdempotent`; `support-request-journeys.test.tsx`, `device-incident-journeys.test.tsx` | API + component |
| Hội thoại/ảnh nhân viên chủ động chọn, preview, đúng request/tenant | `SelfServiceIntegrationTests.EmployeeSelectedImageAndConversationStayInsideTheRequest`, `AttachmentQueryTests`, `AttachmentObjectStoreTests`; `self-service.e2e.ts` | API + browser/component; S3 adapter giả |
| Quota ảnh theo người dùng/đơn vị và nhiều request đồng thời | `AttachmentQuotaTests.ConcurrentUploadsAcrossRequestsShareAnAtomicUserQuota`, `SecurityQueryTests` | API/SQL; lượt PostgreSQL cuối đạt |
| Danh mục ứng dụng, duyệt cài đúng gói được ghim | `SelfServiceIntegrationTests.AdminCatalogPublicationQueuesOnlyThePinnedInstallerForEmployee`, `SelfServiceCommandTests` | API + adapter giả; chưa MSI ứng dụng thật |
| Bảo trì, OTP hash-only/dùng một lần, hết hạn, khóa sau nhiều mã sai | `SelfServiceIntegrationTests.MaintenanceOtpIsHashOnlySingleUseAndQueuesOnlyOneSignedCommand`, `FiveWrongCodesLockTheApprovalAndExpiredCodeCannotBeRedeemed`; `device-protection-flows.test.tsx` | API + component; chưa tạm dừng/gỡ Agent thật |
| Thông báo IT/offline, không trùng, xác nhận thông báo chung | `NotificationDeduplicationTests`, `MonitoringNotificationTests`, `SelfServiceIntegrationTests.CompanyOutageAnnouncementReachesEmployeeAndRecordsAcknowledgement`; `push-security.test.tsx` | API + component; chưa delivery push thật |
| Tài sản, sửa chữa, cho mượn, chi phí/lịch sử và rollback khi audit lỗi | `AssetLifecycleApiTests.RepairAndLoanLifecyclePersistsCostsHistoryAndAuditsWithoutRepeatingTransitions`, `AssetAuditTransactionTests.AuditInsertFailureRollsBackAnAlreadySavedAssetChange` | API; rollback PostgreSQL đã đạt |
| Giới hạn đăng nhập chia sẻ giữa các database scope | `SecurityHardeningTests.ConcurrentDatabaseScopesShareOneAccountBudget` | PostgreSQL, không suy từ limiter trong bộ nhớ |
| Host/VPS chỉ đọc, cấm sai đơn vị, dữ liệu cũ/lỗi được trình bày rõ | `AdminHostMonitoringTests`; `vps-status.e2e.ts`, `vps-status-view.test.tsx`; Python host collector tests | API + parser/component/browser; chưa vận hành VPS thực |
| Điều hướng, bàn phím, focus menu, màn hình hẹp, reduced motion | 5 ca `interface.e2e.ts`; `back-navigation.test.tsx`, `reduced-motion.test.tsx` | Browser/component |
| Nhận diện Windows, số liệu kỹ thuật và version Agent | `OperatingSystemDescriptionTests`, `SystemTelemetryTests`, `EnrollmentFeedbackTests` | Unit + lấy mẫu cục bộ; không đại diện đội máy |
| Agent tự cập nhật: chữ ký, hash, scope, downgrade, nonce, kết quả MSI/dịch vụ | 34 ca `AgentAutoUpdateTests` | Mật mã thật + HTTP/installer adapter giả; chưa nâng cấp MSI có chữ ký trên máy lab |

## Các lỗi kiểm thử và quy trình đã sửa

Lượt PostgreSQL đầu có 182 Passed, 10 Failed / 192; lượt sau có 191 Passed, 1 Failed / 192; lượt cuối có 192 Passed, 0 Failed, 0 NotExecuted / 192. Giữ nguyên kết quả lỗi để truy vết; không xóa test, bỏ qua assertion hoặc nới ràng buộc database.

- `DateTimeOffset.UtcNow.Date` trả `DateTime` không có Kind UTC; khi truyền ngầm trở lại `DateTimeOffset` trên máy UTC+7 sẽ thành offset +07. Hai fixture quota được sửa thành đầu ngày UTC tường minh. Mã nghiệp vụ `AddAttachmentAsync` vốn đã dùng UTC.
- Các lần seed của `AgentRequestSigningTests` dùng cùng tên tổ chức mặc dù tên có unique index. Fixture hiện tạo mã và tên duy nhất cùng một suffix.
- Fixture inventory/notification tạo trạng thái hai máy cùng người dùng. Fixture giải phóng và lưu assignment cũ trước khi chuẩn bị máy tiếp theo, theo unique index hiện có.
- Fixture notification tính dedup key từ timestamp trước khi PostgreSQL làm tròn tới microsecond. Test đọc lại giá trị đã lưu trước khi tính key, đúng nguồn mà reconciliation sử dụng.
- Script npm E2E ở root thiếu `--` nên tham số reporter/output bị npm nhận làm cấu hình. Đã sửa forwarding và xác nhận Playwright liệt kê đủ 13 ca, tạo JSON danh sách ở vị trí yêu cầu.
- Hai script `test-all` build Release nhưng từng để E2E mặc định lấy API Debug. Hiện ghim Release cho lần chạy tổng, giữ tùy chọn API do người dùng chỉ định, xóa kết nối database trong riêng tiến trình browser và phục hồi môi trường ở PowerShell kể cả khi lỗi.
- Bổ sung gate metadata Expo SDK vào hai script kiểm tra tổng; PowerShell nhận `-PythonExecutable` cho máy không có Python trong PATH.

## Coverage và giới hạn diễn giải

| Phạm vi | Lines | Statements | Functions | Branches |
|---|---:|---:|---:|---:|
| Web | 95,49% | 92,79% | 89,07% | 87,50% |
| Mobile | 94,85% | 92,86% | 88,30% | 86,01% |

Độ phủ .NET được hợp nhất theo đường dẫn tuyệt đối và số dòng, lấy hợp của hit giữa 5 tệp Cobertura để không đếm lặp. Báo cáo thô gồm 40.544 dòng, 7.027 được chạy (17,33%); trong đó có 31.756 dòng migrations/model snapshot không chạy trong lượt InMemory và 218 dòng sinh trong `obj`. Nếu tách riêng mã tự viết ngoài hai nhóm đó: 6.941/8.570 dòng, **80,99%**. Đây là thống kê có phạm vi nêu rõ, không phải tuyên bố toàn bộ executable có coverage 80%. Lượt PostgreSQL chạy riêng không thu lại coverage.

## Nguồn bằng chứng cục bộ

Thư mục `.tmp-verification/flow-audit-2026-10-25/` bị Git ignore, chứa:

- `dotnet/*.trx`, `dotnet/*/coverage.cobertura.xml`, `dotnet-tests.log`: baseline .NET và hai ca PostgreSQL NotExecuted.
- `postgres-tests/*.trx`, `postgres-tests.log`: lượt PostgreSQL đầu và lỗi fixture đã tái hiện.
- `postgres-final/*.trx`, `postgres-final.log`: lượt PostgreSQL thứ hai, còn lỗi kỳ vọng timestamp trước khi đọc lại dữ liệu đã lưu.
- `postgres-verified/*.trx`, `postgres-verified.log`: lượt xác minh cuối sau các sửa đổi.
- `web-tests.log`, `mobile-tests.log`, `browser-tests.log`, `mobile-export.log`: kết quả UI/browser và Android bundle.
- `web-build.log`, `dotnet-build.log`, các log typecheck/lint, `fix-build-final.log`, `fix-format-final.log`: các cổng build/chất lượng.
- `deployment-tests.log`, `dependency-audit.log`, `dotnet-dependency-audit.json`, `harness-checks.log`, `browser-forwarding.log`: triển khai, advisory và script kiểm tra.
- `summary.json`: chỉ mục từng test .NET, kết quả, thời gian và tệp TRX chính xác; `source-manifest.json`: SHA-256 các tệp mã/cấu hình trong phạm vi kiểm chứng. Working tree chưa commit, không coi SHA của HEAD là toàn bộ phiên bản đã kiểm tra.

Hai test từng cần PostgreSQL đã chạy đạt trong lượt cuối: `AssetAuditTransactionTests.AuditInsertFailureRollsBackAnAlreadySavedAssetChange` và `SecurityHardeningTests.ConcurrentDatabaseScopesShareOneAccountBudget`. Các ca Agent vẫn được giữ ở kết quả baseline 171/171; không cộng chồng 190 ca API được chạy lại thành số ca duy nhất mới.

Nguồn portable PostgreSQL: [PostgreSQL Windows downloads](https://www.postgresql.org/download/windows/) dẫn tới [EDB binaries](https://www.enterprisedb.com/download-postgresql-binaries). Gói `postgresql-18.6-5-windows-x64-binaries.zip` tải qua HTTPS, SHA-256 cục bộ `E2246BA91D22345BC3D017586C09EDE52D9DF180B1EEB480F050445F1CAD84E2`; không coi hash tự tính là đối chiếu hash nhà phát hành. Các EXE báo NotSigned; Windows cho phép chạy, không tắt Application Control. Database tạm dùng SCRAM, mật khẩu ngẫu nhiên không in ra log, chỉ listen `127.0.0.1`, mỗi factory có schema riêng. Không cài Windows Service; server được dừng sau lượt kiểm tra.

Sau xác minh cuối, xác nhận 0 tiến trình PostgreSQL thuộc thư mục thử nghiệm còn chạy; đã xóa 4 cluster tạm và ZIP tải xuống. Giữ log/TRX, chỉ mục kết quả và các binary portable trong thư mục bị Git ignore để có thể chạy lại. `source-manifest.json` ghi hash 614 tệp trong phạm vi kiểm chứng.

## Nghiệm thu còn cần thiết bị/môi trường thật

- Cài, nâng cấp MSI đã ký, rollback, gỡ Agent, DPAPI dưới tài khoản service Production và heartbeat sau reboot.
- Windows desktop companion, thông báo tới desktop, policy và khóa/cách ly thật trên thiết bị lab được phép với cờ lab rõ ràng.
- APK release trên điện thoại, quyền camera/thư viện ảnh, mạng LAN thật, deep link từ hệ điều hành và push được giao tới máy thật.
- TLS production, Redis/backplane, object storage thật, backup/restore và tải/vận hành dài hạn.

Các mục này chưa được đánh dấu Đạt. Không suy từ unit test/adapter giả, ảnh demo hoặc Android export rằng toàn bộ nghiệm thu thực địa đã hoàn tất.
