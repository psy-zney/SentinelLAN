# Hướng Dẫn Dành Cho Cộng Đồng Mã Nguồn Mở (Open-Source Development Guide)

Chào mừng bạn đến với cộng đồng phát triển mã nguồn mở **SentinelLAN** — Hệ thống quản lý, giám sát và bảo vệ thiết bị đầu cuối cùng node máy chủ mạng nội bộ theo nguyên tắc Zero Trust & Privacy-First.

---

## 1. Triết lý Thiết kế Dự Án

Dự án được xây dựng dựa trên 3 nguyên tắc bất di bất dịch:
1. **Zero Trust ("Never Trust, Always Verify"):**
   - Mọi request đều phải xác thực và phân quyền độc lập.
   - Không tin tưởng ID truyền lên từ Client; luôn xác thực quyền qua `ActorContext` và `OrganizationId` trích xuất từ token.
2. **Quyền riêng tư là trên hết (Privacy-First):**
   - Tuyệt đối **không** xây dựng tính năng gián điệp: không keylogger, không chụp màn hình, không bật camera/micro, không đọc file cá nhân hay lịch sử web.
   - Chỉ thu thập các chỉ số kỹ thuật phục vụ vận hành: CPU, RAM, Disk, phiên bản OS, Agent version và trạng thái mạng.
3. **An toàn điều khiển (Safe Simulation by Default):**
   - Các lệnh nhạy cảm (khóa máy, ngắt mạng) ở chế độ mặc định chỉ là mô phỏng an toàn, không gây phá hủy thiết bị đầu cuối của người dùng.

---

## 2. Cấu Trúc Monorepo & Phân Tầng Trách Nhiệm

Mã nguồn được tổ chức theo kiến trúc **Clean Architecture / Modular Monolith**:

```text
SentinelLAN/
├── apps/
│   ├── backend/                      # Backend API & Nghiệp vụ trung tâm (.NET 10)
│   │   ├── src/
│   │   │   ├── SentinelLAN.Domain/            # Entities, Enums, Value Objects (0 phụ thuộc bên ngoài)
│   │   │   ├── SentinelLAN.Application/       # Use Cases, Services, Interfaces, DTOs, Validation
│   │   │   ├── SentinelLAN.Infrastructure/    # EF Core, PostgreSQL, Hashing, Token, Security
│   │   │   └── SentinelLAN.Api/               # Endpoints, Middlewares, SignalR Hubs, Composition Root
│   │   └── tests/                             # Unit tests, Architecture tests, Integration tests
│   │
│   ├── agent/                        # Endpoint Agent chạy trên thiết bị đầu cuối (.NET 10)
│   │   ├── src/
│   │   │   ├── SentinelLAN.Agent.Core/        # Logic thu thập, hàng đợi offline, an toàn (0 phụ thuộc OS)
│   │   │   ├── SentinelLAN.Agent.Infrastructure/ # DPAPI Windows, Linux adapters, HTTP client
│   │   │   └── SentinelLAN.Agent/             # Worker Service composition & Windows Service host
│   │   └── tests/                             # Kiểm thử Agent & Verifier
│   │
│   └── web/                          # Web Dashboard quản trị (Next.js 16, React 19, Tailwind CSS v4)
│       ├── src/
│       │   ├── app/                           # App Router (Next.js 16)
│       │   ├── components/                    # React UI Components (tiêu chuẩn Accessible, Clean UI)
│       │   ├── hooks/                         # useLiveQuery, useDeviceUpdates (SignalR Realtime)
│       │   ├── lib/                           # Typed API Client, i18n đa ngôn ngữ (VI/EN)
│       │   └── types/                         # TypeScript interfaces & types
│       └── tests/                             # Vitest unit tests & Playwright E2E tests
│
├── deploy/                           # Dockerfiles, scripts cài đặt Windows Service
├── docs/                             # Tài liệu kiến trúc, ADR, hướng dẫn sử dụng
├── scripts/                          # Kịch bản tự động hóa kiểm thử (.ps1 và .sh)
├── compose.yaml                      # Docker Compose stack hoàn chỉnh (Postgres, API, Web, Redis)
└── SentinelLAN.slnx                  # Solution .NET 10 định dạng mới
```

### Quy tắc chiều phụ thuộc bắt buộc:
$$\text{Api} \longrightarrow \text{Infrastructure} \longrightarrow \text{Application} \longrightarrow \text{Domain}$$

- `Domain` không được tham chiếu tới bất kỳ project nào.
- `Application` chỉ tham chiếu `Domain`.
- Không gọi trực tiếp DbContext của module khác; các use case giao tiếp qua Application contract.

---

## 3. Thiết lập Môi trường Phát triển Cục bộ (Local Dev Setup)

### Yêu cầu phần mềm:
- .NET 10 SDK (`10.0.400` trở lên)
- Node.js (`v22` hoặc `v24`) & npm 10+
- Docker & Docker Compose (cho PostgreSQL 18)
- Git

### Các bước khởi động:

1. **Khởi động Database PostgreSQL:**
   ```powershell
   docker compose up -d postgres
   ```

2. **Chạy Backend API:**
   ```powershell
   $env:ASPNETCORE_URLS = "http://localhost:8080"
   $env:ConnectionStrings__SentinelLAN = "Host=localhost;Port=5432;Database=sentinellan;Username=sentinellan;Password=change-me-for-local-development"
   $env:SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY = "development-access-token-signing-key-32-chars-long"
   $env:SENTINELLAN_WEB_ORIGINS = "http://localhost:3000"
   dotnet run --project apps/backend/src/SentinelLAN.Api
   ```

3. **Chạy Web Dashboard:**
   Mở terminal thứ hai:
   ```powershell
   cd apps/web
   npm install
   npm run dev
   ```
   Truy cập Web UI tại: `http://localhost:3000`.

4. **Chạy thử Windows Agent:**
   Mở terminal thứ ba:
   ```powershell
   $env:SENTINELLAN_API_URL = "http://localhost:8080"
   $env:SENTINELLAN_ENROLLMENT_TOKEN = "local-enroll-only"
   dotnet run --project apps/agent/src/SentinelLAN.Agent
   ```

---

## 4. Quy trình Đóng góp (Contribution Workflow)

1. **Fork** repository trên GitHub về tài khoản cá nhân của bạn.
2. Tạo nhánh tính năng mới theo quy ước:
   - `feat/ten-tinh-nang` cho tính năng mới.
   - `fix/ten-loi` cho sửa lỗi.
   - `docs/ten-tai-lieu` cho cập nhật tài liệu.
3. Viết code kèm Unit Test tương ứng.
4. Chạy toàn bộ bộ kiểm thử trước khi commit:
   ```powershell
   ./scripts/test-all.ps1
   ```
   *Tất cả 8 cổng kiểm soát (dotnet format, build, xUnit tests, web lint, typecheck, vitest, build) bắt buộc phải PASS.*
5. Viết Commit Message theo chuẩn **Conventional Commits**:
   - `feat(api): add vps node ssh collector`
   - `fix(agent): handle dpapi encryption edge case`
   - `docs(readme): update deployment guide`
6. Tạo Pull Request (PR) về nhánh `main` của repository gốc kèm mô tả rõ ràng.
