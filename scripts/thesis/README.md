# Sửa và kiểm chứng báo cáo SentinelLAN

Các script này sửa bản báo cáo 17 bảng ngày 23/10/2026 đã áp mẫu bìa. Chúng tạo bản mới và từ chối ghi đè nguồn. Không dùng cho đề cương cũ hoặc một báo cáo có cấu trúc khác.

Dùng Python/Node được trả bởi `load_workspace_dependencies`. `python-docx`, Pillow, pypdf, pdfplumber và pypdfium2 có trong runtime này; không cần cài dependency vào repository.

```powershell
& $python scripts/thesis/build_revision.py --source $sourceDocx --output $revisedDocx --evidence-dir $verifiedResults --screenshots $screenshotsManifest --measurements $measurementsJson
./scripts/thesis/render_report.ps1 -Document $revisedDocx -Python $python
& $python scripts/thesis/verify_report.py --docx $revisedDocx --pdf $revisedPdf --metadata $revisionEvidence --qa $qaDirectory --render
```

`--screenshots` và `--measurements` có thể bỏ qua khi chưa thu được bằng chứng; bản báo cáo sẽ giữ giới hạn tương ứng. Không điền số ví dụ để vượt qua kiểm tra. Ảnh cần tên tệp, tiêu đề, vai trò, mục chèn và thời điểm UTC trong manifest JSON.

`render_report.ps1` dùng Word COM chạy ẩn để giữ bố cục và trường mục lục của mẫu. Trong sandbox Windows, thao tác có thể cần quyền chạy Word ngoài sandbox. Một số Office không hỗ trợ `Font.NameFarEast`; script xử lý bốn slot font bằng OOXML sau khi cập nhật trường. Không bỏ qua bước kiểm tra PDF chỉ vì Word mở được tệp.

Trình kiểm tra trả mã 1 khi có lỗi tài liệu, 0 khi các kiểm tra cấu trúc đạt. Với `--publication`, mã 2 cho biết còn điều kiện nghiệm thu được ghi trong metadata. Mã 0 không thay thế đọc trực quan mọi trang; trình kiểm tra không nhận diện đầy đủ đường nối xuyên chữ bên trong ảnh raster hay chứng minh tác động OS.

Đối chứng hồi quy đã thực hiện: cùng trình kiểm tra phát hiện font Cambria, sai lề mục lục, thiếu thông tin bìa, sai số bảng/hình và mảnh định danh rơi dòng ở bản gốc 56 trang. Bản sửa phải qua các kiểm tra đó.

## Phép đo cục bộ

```powershell
& $python scripts/thesis/measure_local_agent.py --output $newMeasurementDirectory
```

Thư mục đầu ra phải mới. Script dùng hai tiến trình .NET đã build, một API Development/InMemory độc lập, dữ liệu và credential thử, cùng HTTP loopback relay. Sau khi Agent có heartbeat đầu tiên, script đo 300 giây, trả 503 trong 180 giây và quan sát gửi bù. Không phát lệnh OS hoặc sửa mạng vật lý. CPU tiến trình được chuẩn hóa theo số bộ xử lý logic; RAM là working set và private bytes, không phải RAM toàn hệ thống.

Mỗi lần chạy lưu CSV tài nguyên, JSON heartbeat đã loại nội dung telemetry và bản tổng hợp. Các tệp trạng thái chứa credential riêng của phép thử được dọn khi tiến trình đã kết thúc. Lỗi đăng ký hoặc thiếu heartbeat làm thử nghiệm thất bại, không ghi kết quả thành công. Chỉ đưa lần chạy đủ dữ liệu vào báo cáo; giữ nguyên khác biệt giữa phép thử HTTP cục bộ và nghiệm thu PostgreSQL/TLS, mạng vật lý hoặc dài hạn.
