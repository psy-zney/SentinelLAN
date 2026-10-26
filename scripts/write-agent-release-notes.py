"""Write release notes from the exact installer and public build configuration."""
import hashlib
import json
import os
from pathlib import Path
import sys

installer = Path(sys.argv[1])
company = json.loads(Path('deploy/agent/company-release.json').read_text())
with installer.open('rb') as stream:
    checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
print(f'''Bộ cài Windows x64: **SentinelLAN.Setup.exe**. Chỉ cần tải một tệp này, mở bằng quyền quản trị và nhập mã kết nối mới do IT cấp.

- Setup và Agent được build cùng source, cùng phiên bản và ghim máy chủ `{company['serverUrl']}`.
- Command key ID: `{company['commandKeyId']}`. Giữ public key từ bộ cài công ty 0.2.24 đã đối chiếu checksum; không chứa khóa riêng.
- Khắc phục lỗi MSI 1939 của bộ cài 0.2.24: cấu hình service recovery bằng sc.exe sau cài đặt.
- Máy đã có identity dùng luồng khôi phục kết nối, không tiêu thụ lại mã cũ.
- Agent/backend không cần trùng số phiên bản khi cùng giao thức ký request; API tùy chọn 404/405 không chặn heartbeat. Không hạ cấp xác thực để tương thích.
- Biến môi trường/CLI được chỉ định rõ ưu tiên hơn cấu hình mặc định của bộ cài; bản công ty vẫn ghim đúng HTTPS.
- Không phát hành MSI hoặc Agent.exe rời. EXE chứa trọn gói Agent, MSI nội bộ đã sửa và trình cấu hình.
- Kiểm tra tự động: Agent tests trên Linux/Windows, các nhánh enrollment/resume với service mock, đối chiếu cấu hình và hash payload, cài/gỡ gói trên Windows runner. Chưa thay thế nghiệm thu enrollment/heartbeat trên máy nhân viên thực tế.
- Bộ cài chưa ký Authenticode. Link tải trên VPS cũ không tự được cập nhật bởi GitHub Release.

Source commit: `{os.environ['GITHUB_SHA']}`

SHA-256 của `SentinelLAN.Setup.exe`:
```text
{checksum}
```

[Hướng dẫn cài đặt](https://github.com/psy-zney/SentinelLAN/blob/{os.environ['GITHUB_SHA']}/docs/downloads.md)
''')
