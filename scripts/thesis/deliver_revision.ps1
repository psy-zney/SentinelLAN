param(
    [string]$ThesisRoot = 'C:\Users\admin\Documents\SentinelLAN-LuanVan'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$revision = Join-Path $repository 'scratch\thesis-revision-2026-10-24'
$sourceOutput = Join-Path $revision 'output'
$destination = Join-Path $ThesisRoot 'xuat-ban\ban-sua-2026-10-24'
$qaPath = Join-Path $revision 'qa-final\verification.json'
$qa = Get-Content -LiteralPath $qaPath -Raw | ConvertFrom-Json
if (@($qa.errors).Count) { throw 'Document validation failed; refusing delivery.' }
$evidence = Get-Content -LiteralPath (Join-Path $sourceOutput 'revision-evidence.json') -Raw | ConvertFrom-Json
$original = Join-Path $ThesisRoot 'xuat-ban\SentinelLAN-Do-an-tot-nghiep-ban-bien-soan-2026-10-23.docx'
if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash.ToLowerInvariant() -ne $evidence.source_sha256) {
    throw 'The original changed since the revision was created; compare before delivery.'
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in @('SentinelLAN-Do-an-da-sua-2026-10-24.docx', 'SentinelLAN-Do-an-da-sua-2026-10-24.pdf', 'revision-evidence.json')) {
    $sourcePath = Join-Path $sourceOutput $name
    $targetPath = Join-Path $destination $name
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    if ((Get-FileHash -LiteralPath $sourcePath).Hash -ne (Get-FileHash -LiteralPath $targetPath).Hash) {
        throw ('Delivery copy differs: ' + $name)
    }
}
$evidenceTarget = Join-Path $destination 'bang-chung'
New-Item -ItemType Directory -Path $evidenceTarget -Force | Out-Null
foreach ($name in @('measurements.json', 'process-samples.csv', 'heartbeats.json')) {
    Copy-Item -LiteralPath (Join-Path $sourceOutput ('bang-chung\' + $name)) -Destination (Join-Path $evidenceTarget $name) -Force
}
Copy-Item -LiteralPath $qaPath -Destination (Join-Path $evidenceTarget 'verification.json') -Force
Copy-Item -LiteralPath (Join-Path $repository 'docs\verification\thesis-2026-10-24.md') -Destination (Join-Path $evidenceTarget 'doi-chieu-bao-cao.md') -Force
Copy-Item -LiteralPath (Join-Path $repository 'docs\guides\thesis-quality.md') -Destination (Join-Path $destination 'QUY-TAC-CHAT-LUONG.md') -Force
$pictureTarget = Join-Path $destination 'hinh-anh'
New-Item -ItemType Directory -Path $pictureTarget -Force | Out-Null
foreach ($name in @('architecture.png', 'enrollment.png', 'erd.png', 'command.png', 'coverage.png', 'use-cases.png', 'figures.json')) {
    Copy-Item -LiteralPath (Join-Path $sourceOutput ('hinh-anh\' + $name)) -Destination (Join-Path $pictureTarget $name) -Force
}
$screenshotTarget = Join-Path $pictureTarget 'giao-dien'
New-Item -ItemType Directory -Path $screenshotTarget -Force | Out-Null
foreach ($shot in $evidence.screenshots) {
    if ([IO.Path]::GetFileName($shot.file) -ne $shot.file) { throw 'Unexpected screenshot path.' }
    Copy-Item -LiteralPath (Join-Path $revision ('screenshots\' + $shot.file)) -Destination (Join-Path $screenshotTarget $shot.file) -Force
}
Copy-Item -LiteralPath (Join-Path $revision 'screenshots\screenshots.json') -Destination (Join-Path $screenshotTarget 'screenshots.json') -Force
$readme = @"
# Bản sửa báo cáo SentinelLAN ngày 24/10/2026

Word và PDF trong thư mục này là cùng bản đã cập nhật mục lục, gồm $($qa.page_count) trang, 23 bảng và 14 hình (8 ảnh giao diện thật trên dữ liệu demo). Bản gốc ngày 23/10/2026 được giữ nguyên ở thư mục cha.

Đã sửa bìa, font, mục lục, số bảng/hình theo chương, từ điển dữ liệu, sơ đồ và văn phong; bổ sung các cơ chế đã đối chiếu mã nguồn. Phụ lục TC ghi phạm vi bằng chứng, giữ hai ca .NET bỏ qua và giới hạn độ phủ Web/Mobile.

Phép đo 5 phút dùng API và Agent riêng trên Windows, HTTP loopback, Development/InMemory: 180 giây gián đoạn mô phỏng, 5 mẫu gửi bù; 20 heartbeat thành công tương ứng 20 mẫu lưu. Working set Agent 62,27–74,80 MiB; CPU trung bình chuẩn hóa 0,062%; trung vị HTTP bộ chuyển tiếp–API 3,46 ms. Dữ liệu thô nằm trong bang-chung/.

Còn cần nghiệm thu TC24 trên OS thật, PostgreSQL/TLS, mạng vật lý và vận hành dài hạn. Số đo cục bộ không được diễn giải là SLA hoặc bằng chứng triển khai nhiều thiết bị. Tác giả cần xác nhận nội dung cam đoan và hoàn tất kiểm tra học vụ/Turnitin theo quy trình nộp.

Quy tắc ngăn lỗi lặp lại: QUY-TAC-CHAT-LUONG.md và AGENTS.md của không gian viết báo cáo. Script tái tạo/kiểm tra nằm tại $repository\scripts\thesis\; xem README.md trong thư mục đó.
"@
Set-Content -LiteralPath (Join-Path $destination 'DOC-TRUOC-KHI-NOP.md') -Value $readme -Encoding utf8
$agentsPath = Join-Path $ThesisRoot 'AGENTS.md'
$agentsText = Get-Content -LiteralPath $agentsPath -Raw
$sectionTitle = '## Kiểm soát chất lượng sau audit ngày 24/10/2026'
if (-not $agentsText.Contains($sectionTitle)) {
    Copy-Item -LiteralPath $agentsPath -Destination (Join-Path $evidenceTarget 'AGENTS-truoc-bo-sung.md') -Force
    $rules = @'

## Kiểm soát chất lượng sau audit ngày 24/10/2026

- Khi sửa báo cáo, áp dụng `C:\Users\admin\MyProject\SentinelLAN\docs\guides\thesis-quality.md` và bộ tạo/kiểm tra tại `scripts/thesis/` trong kho SentinelLAN. Phải sửa nguồn, xuất lại, cập nhật trường và đọc bản PDF cuối; không coi thay font hay thay ảnh trong nguồn là bằng chứng PDF đã đúng.
- Dùng Times New Roman cho cả bốn slot font của style, run, field và hyperlink. Mục lục cấp chương in đậm 13 pt tại lề trái. Bảng/hình đánh số theo chương hoặc chữ phụ lục, có dấu hai chấm. Bảng ngắn không chia vụn; tên định danh không được rơi đuôi 2–4 ký tự.
- Sơ đồ có chữ tối thiểu 10 pt khi in, ít nhất 300 DPI, nhãn nền trắng và đường nối tránh chữ. Khi Word có SVG thay thế, phải thay/cập nhật cả biểu diễn SVG và PNG; kiểm tra ảnh nhúng và ảnh thực sự hiển thị trong PDF.
- Dữ liệu kiểm thử chỉ được báo Đạt trong phạm vi ca thực tế có bằng chứng. Giữ ca bỏ qua và cổng độ phủ thất bại. Tách fixture, HTTP cục bộ/InMemory, PostgreSQL/TLS và nghiệm thu OS. Không dùng số liệu mẫu trong audit làm số đo thực nghiệm.
- Mỗi phép đo lưu mẫu thô, phương pháp, môi trường, thời điểm và phiên bản. Đọc RAM riêng tiến trình Agent; không thay bằng RAM hệ thống. Độ trễ HTTP cục bộ không phải độ trễ Agent–Dashboard hoặc SLA.
- Xác minh đường dẫn API, tham số bảo mật và metadata tài liệu bằng nguồn gốc. Các năm không xác định giữ n.d.; ngày phát hành một sản phẩm không làm mọi trang tài liệu của sản phẩm có cùng năm.
- Học vị GVHD đã được đối chiếu danh sách giảng viên UEH công bố: TS. Nguyễn An Tế; tên ngành chuẩn hóa trên bìa là Công nghệ thông tin. Nguồn đối chiếu: https://www.ueh.edu.vn/images/upload/editer/20250815083859_dsGiangvientoanthoigian31122024.pdf. Thông tin này bổ sung cho mẫu bìa vốn không ghi học vị; không tự điền lớp.
- Giữ màu logo UEH gốc, hoa văn bìa, bản gốc và hồ sơ kiểm chứng riêng. Bản mới được đặt tên ngày sửa; không gọi là bản nộp đã đủ mọi điều kiện nếu còn nghiệm thu hoặc xác nhận học vụ chưa hoàn tất.
'@
    [IO.File]::AppendAllText($agentsPath, "`r`n" + $rules + "`r`n", [Text.UTF8Encoding]::new($false))
}
[pscustomobject]@{ Destination = $destination; Pages = $qa.page_count; ValidationErrors = @($qa.errors).Count; OriginalPreserved = $true; Rules = $agentsPath } | ConvertTo-Json -Compress
